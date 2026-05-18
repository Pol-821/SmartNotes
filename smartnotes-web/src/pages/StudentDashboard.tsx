import { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { api } from '@/services/api';
import { BookOpen, LogOut, Search, Folder, GraduationCap, ArrowRight, Loader2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { toast } from 'sonner';

export default function StudentDashboard() {
  const navigate = useNavigate();
  const [isLoading, setIsLoading] = useState(true);
  const [isJoining, setIsJoining] = useState(false);
  const [classCode, setClassCode] = useState('');
  const [enrolledClasses, setEnrolledClasses] = useState<any[]>([]);

  // 1. Carregar les classes on l'alumne ja està matriculat
  const fetchEnrolledClasses = async () => {
    try {
      setIsLoading(true);
      const response = await api.get('/classroom/enrolled');
      setEnrolledClasses(response.data);
    } catch (error) {
      console.error("Error carregant matrícules:", error);
      toast.error("No s'han pogut carregar les teves classes");
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => {
    fetchEnrolledClasses();
  }, []);

  // 2. Lògica per unir-se a una classe nova
  const handleJoinClass = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!classCode.trim()) return;

    try {
      setIsJoining(true);
      await api.post('/classroom/join', { code: classCode.toUpperCase() });
      
      toast.success("T'has unit a la classe correctament!");
      setClassCode('');
      fetchEnrolledClasses(); // Recarreguem la llista per veure la nova targeta
    } catch (error: any) {
      const msg = error.response?.data?.error || "Codi invàlid";
      toast.error(msg);
    } finally {
      setIsJoining(false);
    }
  };

  const handleLogout = () => {
    localStorage.removeItem('token');
    navigate('/login');
  };

  if (isLoading) {
    return (
      <div className="min-h-screen flex items-center justify-center bg-slate-50">
        <Loader2 className="h-8 w-8 text-blue-600 animate-spin" />
      </div>
    );
  }

  return (
    <div className="min-h-screen bg-slate-50 flex flex-col">
      
      {/* CAPÇALERA DE L'ALUMNE */}
      <header className="h-16 bg-white border-b border-slate-200 flex items-center justify-between px-8 shadow-sm">
        <div className="flex items-center gap-2">
          <div className="bg-blue-600 p-1.5 rounded-lg">
            <BookOpen className="h-5 w-5 text-white" />
          </div>
          <span className="font-bold text-xl text-slate-900 tracking-tight">SmartNotes <span className="text-blue-600 font-medium text-sm ml-1">Alumne</span></span>
        </div>
        
        <Button variant="ghost" onClick={handleLogout} className="text-slate-500 hover:text-red-600">
          <LogOut className="h-4 w-4 mr-2" />
          Sortir
        </Button>
      </header>

      <main className="max-w-6xl mx-auto w-full p-8 space-y-12">
        
        {/* ZONA D'UNIÓ A CLASSE */}
        <section className="bg-white rounded-2xl p-8 border border-slate-200 shadow-sm flex flex-col md:flex-row items-center justify-between gap-8">
          <div className="space-y-2 text-center md:text-left">
            <h2 className="text-2xl font-bold text-slate-900">Uneix-te a una nova classe</h2>
            <p className="text-slate-500">Introdueix el codi de 6 dígits que t'ha donat el teu professor.</p>
          </div>
          
          <form onSubmit={handleJoinClass} className="flex gap-3 w-full md:w-auto">
            <Input 
              placeholder="Ex: RIFBW2" 
              className="font-mono uppercase tracking-widest text-center text-lg h-12 w-full md:w-48 border-2 focus:border-blue-500"
              value={classCode}
              onChange={(e) => setClassCode(e.target.value.toUpperCase())}
              maxLength={6}
            />
            <Button type="submit" className="h-12 px-6 bg-blue-600 hover:bg-blue-700" disabled={isJoining}>
              {isJoining ? <Loader2 className="h-5 w-5 animate-spin" /> : "Unir-me"}
            </Button>
          </form>
        </section>

        {/* LLISTAT DE CLASSES MATRICULADES */}
        <section className="space-y-6">
          <div className="flex items-center gap-2 border-l-4 border-blue-600 pl-4">
            <GraduationCap className="h-6 w-6 text-blue-600" />
            <h3 className="text-xl font-bold text-slate-900">Les Meves Assignatures</h3>
          </div>

          {enrolledClasses.length === 0 ? (
            <div className="text-center py-20 bg-slate-100/50 rounded-3xl border-2 border-dashed border-slate-200">
              <Search className="h-12 w-12 text-slate-300 mx-auto mb-4" />
              <p className="text-slate-500 font-medium">Encara no t'has unit a cap classe.</p>
              <p className="text-slate-400 text-sm">Demana el codi al teu professor per començar.</p>
            </div>
          ) : (
            <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
              {enrolledClasses.map((cls) => (
                <Card 
                  key={cls.id} 
                  className="group cursor-pointer hover:shadow-xl transition-all duration-300 border-0 bg-white overflow-hidden flex flex-col h-48"
                  onClick={() => navigate(`/student/class/${cls.id}`)}
                >
                  <div 
                    className="h-2 w-full" 
                    style={{ backgroundColor: cls.color }}
                  />
                  <div className="p-6 flex flex-col justify-between flex-1">
                    <div>
                      <div className="flex items-center justify-between mb-2">
                        <Folder className="h-5 w-5" style={{ color: cls.color }} />
                        <span className="text-[10px] font-bold text-slate-400 uppercase tracking-widest">Matriculat</span>
                      </div>
                      <h4 className="text-xl font-bold text-slate-900 group-hover:text-blue-600 transition-colors">
                        {cls.name}
                      </h4>
                    </div>
                    
                    <div className="flex items-center text-blue-600 font-semibold text-sm">
                      Veure apunts
                      <ArrowRight className="ml-2 h-4 w-4 group-hover:translate-x-1 transition-transform" />
                    </div>
                  </div>
                </Card>
              ))}
            </div>
          )}
        </section>
      </main>
    </div>
  );
}