import { useState, useEffect } from 'react';
import { api } from '@/services/api';
import { Cpu, Server, Plus, Trash2, Copy, Loader2, Factory, Settings2, KeyRound, ArrowLeft } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { toast } from 'sonner';

export default function RaspberryScreen() {
  const [devices, setDevices] = useState<any[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [serialNumber, setSerialNumber] = useState('');
  const [isLinking, setIsLinking] = useState(false);
  const [isAdmin, setIsAdmin] = useState(false);

  // Modals
  const [targetSerial, setTargetSerial] = useState<string | null>(null);
  const [isUnlinkModalOpen, setIsUnlinkModalOpen] = useState(false);
  const [newDeviceData, setNewDeviceData] = useState<{ serial: string } | null>(null);

  useEffect(() => {
    fetchDevices();
  }, []);

  const fetchDevices = async () => {
    setIsLoading(true);
    try {
      const [devicesRes, adminRes] = await Promise.all([
        api.get('/raspberry/my-devices'),
        api.get('/raspberry/is-admin')
      ]);
      setDevices(devicesRes.data);
      setIsAdmin(adminRes.data.isAdmin);
    } catch (error) {
      toast.error("No s'han pogut carregar les dades");
    } finally {
      setIsLoading(false);
    }
  };

  // 1. Vincular
  const handleLinkDevice = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!serialNumber.trim()) return;
    try {
      setIsLinking(true);
      await api.post('/raspberry/register', { serialNumber: serialNumber.trim() });
      toast.success("Raspberry vinculada correctament!");
      setSerialNumber('');
      fetchDevices();
    } catch (error: any) {
      toast.error(error.response?.data || "No s'ha pogut vincular.");
    } finally {
      setIsLinking(false);
    }
  };

  // 2. Desvincular
  const executeUnlink = async () => {
    if (!targetSerial) return;
    try {
      await api.post('/raspberry/unregister', { serialNumber: targetSerial });
      toast.success("Dispositiu desvinculat");
      setIsUnlinkModalOpen(false);
      fetchDevices();
    } catch (error) {
      toast.error("Error al desvincular");
    }
  };

  // 3. FABRICAR (SuperAdmin)
  const handleProvisionDevice = async () => {
    try {
      const response = await api.post('/raspberry/provision');
      setNewDeviceData({
        serial: response.data.serialNumber
      });
      setSerialNumber(response.data.serialNumber); 
    } catch (error) {
      toast.error("Error al fabricar la màquina");
    }
  };

  const copyToClipboard = (text: string) => {
    navigator.clipboard.writeText(text);
    toast.success("Copiat al porta-retalls!");
  };

  if (isLoading) return <div className="flex justify-center py-20"><Loader2 className="h-8 w-8 text-blue-600 animate-spin" /></div>;

  return (
    // FIX VISUAL: pt-10 afegeix el marge superior necessari perquè no toqui el sostre
    <div className="max-w-5xl mx-auto space-y-8 relative pt-10 pb-12">
      
      {/* BOTÓ DE TORNAR ENRERE (NOU) */}
      <button 
        onClick={() => window.location.href = '/notes'} 
        className="group flex items-center text-sm font-medium text-slate-500 hover:text-blue-600 mb-2 transition-colors"
      >
        <ArrowLeft className="mr-2 h-4 w-4 group-hover:-translate-x-1 transition-transform" />
        Tornar a Tots els Apunts
      </button>
      
      {/* CAPÇALERA AMB NOU MARGE */}
      <div>
        <div className="flex items-center gap-3 mb-2">
          <div className="p-2 bg-slate-900 rounded-lg">
            <Cpu className="h-6 w-6 text-emerald-400" />
          </div>
          <h1 className="text-3xl font-bold text-slate-900">La meva Raspberry Pi</h1>
        </div>
        <p className="text-slate-500">Gestiona els dispositius de gravació físics vinculats a la teva aula.</p>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-8">
        {/* COLUMNA ESQUERRA */}
        <div className="lg:col-span-1 space-y-6">
          <Card className="p-6 border-slate-200 shadow-sm bg-white">
            <div className="flex items-center gap-2 mb-4 text-slate-900 font-semibold">
              <Plus className="h-5 w-5 text-blue-600" />
              Vincular dispositiu
            </div>
            <p className="text-sm text-slate-500 mb-4">Introdueix el Codi de Vinculació per connectar la màquina al teu compte.</p>
            <form onSubmit={handleLinkDevice} className="space-y-3">
              <Input 
                placeholder="Ex: RPI-2026-X89..." 
                className="font-mono uppercase text-center tracking-wider"
                value={serialNumber}
                onChange={(e) => setSerialNumber(e.target.value.toUpperCase())}
              />
              <Button type="submit" className="w-full bg-slate-900 hover:bg-slate-800" disabled={isLinking}>
                {isLinking ? <Loader2 className="h-4 w-4 animate-spin mr-2" /> : "Vincular"}
              </Button>
            </form>
          </Card>

          {/* NOMÉS ES MOSTRA SI ETS EL SUPERADMIN */}
          {isAdmin && (
            <Card className="p-5 border-blue-200 border-2 border-dashed bg-blue-50">
              <h4 className="font-semibold text-blue-800 flex items-center mb-2 text-sm">
                <Factory className="h-4 w-4 mr-2" /> Eines SuperAdmin
              </h4>
              <p className="text-xs text-blue-600/80 mb-4">
                Genera una Raspberry Pi a la base de dades per obtenir els codis d'instal·lació.
              </p>
              <Button variant="outline" className="w-full text-xs font-bold border-blue-300 text-blue-700 bg-white" onClick={handleProvisionDevice}>
                <Settings2 className="h-3 w-3 mr-2" /> Fabricar Màquina
              </Button>
            </Card>
          )}
        </div>

        {/* COLUMNA DRETA: LLISTA */}
        <div className="lg:col-span-2 space-y-4">
          <h2 className="text-xl font-bold text-slate-900 mb-4">Dispositius Actius</h2>
          
          {devices.length === 0 ? (
            <Card className="flex flex-col items-center justify-center py-16 text-center border-dashed border-2 bg-slate-50/50 shadow-none">
              <Server className="h-12 w-12 text-slate-300 mb-3" />
              <h3 className="text-lg font-semibold text-slate-700">Cap Raspberry vinculada</h3>
            </Card>
          ) : (
            devices.map((device) => (
              <Card key={device.serialNumber} className="p-6 border-slate-200 shadow-sm bg-white flex flex-col sm:flex-row justify-between items-start sm:items-center gap-4">
                <div className="flex items-start gap-4">
                  <div className="p-3 bg-emerald-100 rounded-full text-emerald-600 mt-1">
                    <Cpu className="h-6 w-6" />
                  </div>
                  <div>
                    <h3 className="font-bold text-lg text-slate-900 flex items-center gap-2">
                      Microòfon Principal
                      <span className="px-2 py-0.5 rounded-full bg-emerald-100 text-emerald-700 text-[10px] font-bold uppercase tracking-wider">Actiu</span>
                    </h3>
                    {/* El professor només veu el Codi de Vinculació */}
                    <p className="text-sm font-mono text-slate-500 mt-1">Codi: {device.serialNumber}</p>
                  </div>
                </div>

                <div className="flex sm:flex-col gap-2 w-full sm:w-auto mt-4 sm:mt-0">
                  <Button 
                    variant="outline" 
                    size="sm" 
                    className="border-red-200 text-red-600 hover:bg-red-50 hover:text-red-700"
                    onClick={() => { setTargetSerial(device.serialNumber); setIsUnlinkModalOpen(true); }}
                  >
                    <Trash2 className="h-4 w-4 mr-2" /> Desvincular
                  </Button>
                </div>
              </Card>
            ))
          )}
        </div>
      </div>

      {/* --- MODAL DE DESVINCULAR (PER A PROFESSORS) --- */}
      {isUnlinkModalOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/50 backdrop-blur-sm p-4">
          <Card className="w-full max-w-sm p-6 bg-white animate-in zoom-in-95 duration-200">
            <h3 className="text-xl font-bold text-slate-900 mb-2">Desvincular Dispositiu</h3>
            <p className="text-sm text-slate-500 mb-6">La Raspberry deixarà de pujar apunts al teu compte. Estàs segur?</p>
            <div className="flex justify-end gap-3">
              <Button variant="outline" onClick={() => setIsUnlinkModalOpen(false)}>Cancel·lar</Button>
              <Button className="bg-red-600 hover:bg-red-700 text-white" onClick={executeUnlink}>Sí, Desvincular</Button>
            </div>
          </Card>
        </div>
      )}

      {/* --- MODAL DE FABRICACIÓ (NOMÉS SUPERADMIN) --- */}
      {newDeviceData && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/60 backdrop-blur-md p-4">
          <Card className="w-full max-w-md p-6 bg-white shadow-2xl animate-in zoom-in-95 duration-200">
            <div className="mx-auto w-12 h-12 bg-emerald-100 text-emerald-600 rounded-full flex items-center justify-center mb-4">
              <KeyRound className="h-6 w-6" />
            </div>
            <h3 className="text-xl font-bold text-center text-slate-900 mb-2">Dispositiu Fabricat</h3>
            <p className="text-sm text-center text-slate-500 mb-6">
              La Raspberry necessita l'API Key global configurada al servidor i aquest codi de vinculació.
            </p>
            
            <div className="mb-6">
              <label className="text-xs font-bold text-slate-500 uppercase tracking-wider">Codi de Vinculació</label>
              <div className="flex items-center bg-slate-100 p-2 rounded-lg mt-1 border border-slate-200">
                <code className="flex-1 font-mono text-sm text-slate-900">{newDeviceData.serial}</code>
                <Button variant="ghost" size="icon" onClick={() => copyToClipboard(newDeviceData.serial)} className="h-6 w-6">
                  <Copy className="h-4 w-4" />
                </Button>
              </div>
            </div>

            <Button className="w-full bg-slate-900 hover:bg-slate-800" onClick={() => setNewDeviceData(null)}>
              Tancar
            </Button>
          </Card>
        </div>
      )}
    </div>
  );
}