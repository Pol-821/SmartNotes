import { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { api } from '@/services/api';
import { FileText, Sparkles, Search, Calendar, ChevronRight, Timer } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { FullPageLoader } from '@/components/ui/spinner';
import { toast } from 'sonner';

export default function DashboardScreen() {
  const navigate = useNavigate();
  const [isLoading, setIsLoading] = useState(true);
  const [notes, setNotes] = useState<any[]>([]);
  const [searchQuery, setSearchQuery] = useState('');
  const [userProfile, setUserProfile] = useState<any>(null);

  const fetchData = async () => {
    try {
      setIsLoading(true);

      const profileResponse = await api.get('/user/me');
      setUserProfile(profileResponse.data);

      const notesResponse = await api.get('/notes?page=1&pageSize=50');
      const notesArray = notesResponse.data.items || notesResponse.data.Items || [];
      setNotes(notesArray);

    } catch (error: any) {
      console.error("Error carregant el Dashboard:", error);
      toast.error("No s'han pogut carregar les teves dades");
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => {
    fetchData(); 
  }, []);

  // REVISIÓ AUTOMÀTICA (POLLING)
  useEffect(() => {
    const hasProcessing = notes.some(note => note.content?.includes('[⏳'));
    if (!hasProcessing) return;
    
    const interval = setInterval(async () => {
      try {
        const response = await api.get('/notes?page=1&pageSize=50');
        const notesArray = response.data.items || response.data.Items || [];
        setNotes(notesArray);
      } catch (err) {
        console.error("Error polling notes:", err);
      }
    }, 5000); 

    return () => clearInterval(interval); 
  }, []); // <-- només una vegada al mount, no depèn de notes

  // Lògica del Cercador i FILTRATGE DE NOTES PENDENTS
  // Només mostrem a la graella les notes que NO tenen l'etiqueta de processament
  const completedNotes = notes.filter(note => !note.content?.includes('[⏳'));
  
  const filteredNotes = completedNotes.filter(note => 
    note.title?.toLowerCase().includes(searchQuery.toLowerCase())
  );

  if (isLoading) {
    return <FullPageLoader text="Sincronitzant dades amb el servidor..." />;
  }

  return (
    <div className="max-w-6xl mx-auto space-y-8">
      
      {/* CAPÇALERA */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
        <div>
          <h1 className="text-3xl font-bold tracking-tight text-slate-900">Els Meus Apunts</h1>
          <p className="text-slate-500 mt-1">Gestiona i revisa les transcripcions de les teves classes.</p>
        </div>
      </div>

      {/* GRÀFICA DE CONSUM D'IA */}
      {userProfile && (
        <Card className="p-6 bg-gradient-to-r from-slate-900 to-slate-800 text-white shadow-lg border-none">
          <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 mb-4">
            <div className="flex items-center gap-2">
              <div className="p-2 bg-blue-500/20 rounded-lg">
                <Timer className="h-5 w-5 text-blue-400" />
              </div>
              <div>
                <h3 className="font-semibold text-lg text-white">Temps de Transcripció</h3>
                <p className="text-slate-400 text-sm">
                  {userProfile.planName ? `Pla ${userProfile.planName}` : `Pla ${userProfile.secondsAvailable > 14400 ? 'Pro' : 'Free'}`}
                </p>
              </div>
            </div>
            <div className="text-right">
              <span className="text-2xl font-bold text-white">{Math.floor(userProfile.secondsAvailable / 60)}</span>
              <span className="text-slate-400"> / {Math.floor(userProfile.maxSeconds / 60)} min restants</span>
            </div>
          </div>
          
          <div className="w-full bg-slate-700/50 rounded-full h-3 mb-2 overflow-hidden">
            <div 
              className={`h-full rounded-full transition-all duration-500 ${
                (userProfile.secondsAvailable / userProfile.maxSeconds) < 0.2 ? 'bg-red-500' : 'bg-blue-500'
              }`}
              style={{ width: `${Math.min(Math.max((userProfile.secondsAvailable / userProfile.maxSeconds) * 100, 0), 100)}%` }}
            />
          </div>
          <p className="text-xs text-slate-400">
            Cada vegada que la Raspberry Pi puja una classe, els minuts es descompten automàticament.
          </p>
        </Card>
      )}

      {/* ZONA DE BUSCADOR */}
      {completedNotes.length > 0 && (
        <div className="relative max-w-md">
          <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-slate-400" />
          <Input 
            placeholder="Buscar per títol de la classe..." 
            className="pl-10 bg-white border-slate-200"
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
          />
        </div>
      )}

      {/* LÒGICA DE VISUALITZACIÓ */}
      {completedNotes.length === 0 ? (
        <Card className="flex flex-col items-center justify-center py-24 text-center border-dashed border-2 bg-slate-50/50 shadow-none">
          <div className="bg-white p-4 rounded-full shadow-sm mb-4 border border-slate-100">
            <Sparkles className="h-10 w-10 text-blue-500" strokeWidth={1.5} />
          </div>
          <h3 className="text-xl font-semibold text-slate-900 mb-2">Tot a punt per començar</h3>
          <p className="text-slate-500 max-w-sm mb-6">
            Encén el teu dispositiu <strong>SmartNotes (Raspberry Pi)</strong>, prem el botó per gravar la teva classe, i la IA farà aparèixer els teus apunts aquí mateix.
          </p>
          <Button variant="outline" className="bg-white" onClick={fetchData}>
            <Search className="mr-2 h-4 w-4 text-slate-400" />
            Comprovar si hi ha classes noves
          </Button>
        </Card>

      ) : filteredNotes.length === 0 ? (
        <div className="text-center py-12 text-slate-500">
          No s'han trobat apunts que coincideixin amb "{searchQuery}".
        </div>
      ) : (
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
          {filteredNotes.map((note) => (
            <Card 
              key={note.id} 
              className="group cursor-pointer hover:border-blue-500 hover:shadow-md transition-all duration-200 bg-white flex flex-col"
              onClick={() => navigate(`/notes/${note.id}`)}
            >
              <div className="p-6 space-y-4 flex-1">
                <div className="flex justify-between items-start">
                  <div className="p-2 bg-blue-50 rounded-lg text-blue-600 group-hover:bg-blue-600 group-hover:text-white transition-colors">
                    <FileText className="h-6 w-6" />
                  </div>
                  <ChevronRight className="text-slate-300 group-hover:text-blue-500 transition-colors" />
                </div>
                
                <div>
                  <h3 className="font-semibold text-lg text-slate-900 line-clamp-2 leading-tight">
                    {note.title || "Sense títol"}
                  </h3>
                </div>
              </div>
              
              <div className="px-6 py-4 border-t border-slate-100 mt-auto">
                <div className="flex items-center justify-between text-sm text-slate-500">
                  <div className="flex items-center gap-1.5">
                    <Calendar className="h-4 w-4" />
                    <span>{note.createdAt ? new Date(note.createdAt).toLocaleDateString('ca-ES') : 'Avui'}</span>
                  </div>
                </div>
              </div>
            </Card>
          ))}
        </div>
      )}
      
    </div>
  );
}