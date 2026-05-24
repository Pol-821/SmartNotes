import { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { api } from '@/services/api';
import { Settings, Lock, Save, Languages, Loader2, ArrowLeft, Check } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card, CardHeader, CardTitle, CardDescription, CardContent } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { FullPageLoader } from '@/components/ui/spinner';
import { toast } from 'sonner';

const AVAILABLE_LANGUAGES = [
  { code: 'ca', name: 'Català' },
  { code: 'es', name: 'Castellà' },
  { code: 'en', name: 'Anglès' },
  { code: 'fr', name: 'Francès' },
  { code: 'it', name: 'Italià' },
  { code: 'de', name: 'Alemany' },
];

export default function SettingsScreen() {
  const navigate = useNavigate();
  const [isLoading, setIsLoading] = useState(true);
  
  // Array per gestionar els idiomes visualment
  const [selectedLangs, setSelectedLangs] = useState<string[]>(['ca', 'es']);
  
  // Estats de contrasenya
  const [currentPassword, setCurrentPassword] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');

  useEffect(() => {
    const fetchUserData = async () => {
      try {
        setIsLoading(true);
        const response = await api.get('/user/me');
        if (response.data.preferredLanguage) {
          setSelectedLangs(response.data.preferredLanguage.split(','));
        }
      } catch (error) {
        console.error("No s'han pogut carregar les preferències");
      } finally {
        setIsLoading(false);
      }
    };
    fetchUserData();
  }, []);

  // Funció per seleccionar/deseleccionar idiomes fent clic
  const toggleLanguage = (code: string) => {
    if (selectedLangs.includes(code)) {
      if (selectedLangs.length === 1) {
        toast.warning("Has de tenir com a mínim un idioma seleccionat.");
        return;
      }
      setSelectedLangs(selectedLangs.filter(l => l !== code));
    } else {
      setSelectedLangs([...selectedLangs, code]);
    }
  };

  const handleSaveSettings = async (e: React.FormEvent) => {
    e.preventDefault();
    
    if (newPassword && newPassword !== confirmPassword) {
      toast.error("Les contrasenyes noves no coincideixen");
      return;
    }

    try {
      setIsLoading(true);
      await api.put('/user/settings', {
        preferredLanguage: selectedLangs.join(','), // Ho convertim a text per l'API (ex: "ca,es")
        currentPassword: currentPassword || null,
        newPassword: newPassword || null
      });
      
      toast.success("Configuració desada correctament");
      setCurrentPassword('');
      setNewPassword('');
      setConfirmPassword('');
    } catch (error: any) {
      const msg = error.response?.data?.error || "Error al desar la configuració";
      toast.error(msg);
    } finally {
      setIsLoading(false);
    }
  };

  if (isLoading) return <FullPageLoader text="Carregant preferències..." />;

  return (
    <div className="max-w-4xl mx-auto space-y-6 pt-6 pb-12 px-4 sm:px-0">
      
      {/* BOTÓ DE TORNAR */}
      <button 
        onClick={() => navigate('/notes')} 
        className="group flex items-center text-sm font-medium text-slate-500 hover:text-blue-600 transition-colors"
      >
        <ArrowLeft className="mr-2 h-4 w-4 group-hover:-translate-x-1 transition-transform" />
        Tornar a Tots els Apunts
      </button>

      <div>
        <div className="flex items-center gap-3 mb-2">
          <div className="p-2 bg-slate-900 rounded-lg">
            <Settings className="h-6 w-6 text-blue-400" />
          </div>
          <h1 className="text-3xl font-bold text-slate-900">Configuració</h1>
        </div>
        <p className="text-slate-500">Gestiona les teves preferències personals i la seguretat del compte.</p>
      </div>

      <form onSubmit={handleSaveSettings} className="space-y-6">
        
        {/* SECCIÓ: IDIOMES DE L'IA (INTERACTIU) */}
        <Card className="border-slate-200 shadow-sm overflow-hidden">
          <CardHeader className="bg-slate-50/50 border-b border-slate-100">
            <div className="flex items-center gap-2">
              <Languages className="h-5 w-5 text-blue-600" />
              <CardTitle className="text-lg">Idiomes de Transcripció</CardTitle>
            </div>
            <CardDescription>
              Selecciona quins idiomes fas servir a les teves classes perquè l'IA estigui preparada.
            </CardDescription>
          </CardHeader>
          <CardContent className="pt-6">
            <div className="grid grid-cols-2 sm:grid-cols-3 gap-3">
              {AVAILABLE_LANGUAGES.map((lang) => {
                const isSelected = selectedLangs.includes(lang.code);
                return (
                  <button
                    key={lang.code}
                    type="button"
                    onClick={() => toggleLanguage(lang.code)}
                    className={`flex items-center justify-between p-3 rounded-lg border-2 transition-all ${
                      isSelected 
                        ? 'border-blue-600 bg-blue-50 text-blue-800' 
                        : 'border-slate-200 bg-white text-slate-600 hover:border-slate-300'
                    }`}
                  >
                    <span className="font-medium text-sm">{lang.name}</span>
                    {isSelected && <Check className="h-4 w-4 text-blue-600" />}
                  </button>
                );
              })}
            </div>
          </CardContent>
        </Card>

        {/* SECCIÓ: SEGURETAT */}
        <Card className="border-slate-200 shadow-sm overflow-hidden">
          <CardHeader className="bg-slate-50/50 border-b border-slate-100">
            <div className="flex items-center gap-2">
              <Lock className="h-5 w-5 text-amber-600" />
              <CardTitle className="text-lg">Seguretat de la Contrasenya</CardTitle>
            </div>
            <CardDescription>
              Per canviar la teva contrasenya actual, primer has de verificar la teva identitat.
            </CardDescription>
          </CardHeader>
          <CardContent className="pt-6 space-y-6">
            
            <div className="space-y-2 max-w-sm">
              <Label htmlFor="currentPassword">Contrasenya Actual</Label>
              <Input 
                id="currentPassword" type="password" placeholder="••••••••"
                value={currentPassword} onChange={(e) => setCurrentPassword(e.target.value)}
              />
            </div>

            <div className="grid grid-cols-1 md:grid-cols-2 gap-6 pt-4 border-t border-slate-100">
              <div className="space-y-2">
                <Label htmlFor="newPassword">Nova Contrasenya</Label>
                <Input 
                  id="newPassword" type="password" placeholder="••••••••"
                  value={newPassword} onChange={(e) => setNewPassword(e.target.value)}
                  disabled={!currentPassword} // Bloquegem si no ha posat l'actual
                />
              </div>
              <div className="space-y-2">
                <Label htmlFor="confirmPassword">Confirmar Nova Contrasenya</Label>
                <Input 
                  id="confirmPassword" type="password" placeholder="••••••••"
                  value={confirmPassword} onChange={(e) => setConfirmPassword(e.target.value)}
                  disabled={!currentPassword}
                />
              </div>
            </div>
            {!currentPassword && <p className="text-xs text-amber-600 mt-2">Introdueix la contrasenya actual per poder escriure'n una de nova.</p>}
          </CardContent>
        </Card>

        <div className="flex justify-end pt-4">
          <Button type="submit" className="bg-blue-600 hover:bg-blue-700 h-12 px-8 shadow-lg shadow-blue-600/20" disabled={isLoading}>
            {isLoading ? <Loader2 className="h-4 w-4 animate-spin mr-2" /> : <Save className="h-4 w-4 mr-2" />}
            Desar tots els canvis
          </Button>
        </div>

      </form>
    </div>
  );
}