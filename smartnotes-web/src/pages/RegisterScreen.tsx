import { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { api } from '@/services/api';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Card } from '@/components/ui/card';
import { BookOpen, GraduationCap, ArrowLeft, Check } from 'lucide-react';
import { toast } from 'sonner';

export default function RegisterScreen() {
  const navigate = useNavigate();
  const [isLoading, setIsLoading] = useState(false);

  // Estats del formulari
  const [username, setUsername] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  
  // Estats dels nous requisits
  const [role, setRole] = useState<'alumne' | 'professor' | null>(null);
  const [languages, setLanguages] = useState<string[]>([]);

  useEffect(() => {
    const token = localStorage.getItem('token');
    const role = localStorage.getItem('role');
    
    if (token) {
      if (role === 'alumne') navigate('/student');
      else navigate('/notes');
    }
  }, [navigate]);

  const toggleLanguage = (lang: string) => {
    if (languages.includes(lang)) {
      setLanguages(languages.filter(l => l !== lang));
    } else {
      setLanguages([...languages, lang]);
    }
  };

  const handleRegister = async (e: React.FormEvent) => {
    e.preventDefault();
    
    if (!role) {
      toast.error("Selecciona un rol", { description: "Has de triar si ets alumne o professor." });
      return;
    }

    if (role === 'professor' && languages.length === 0) {
      toast.error("Selecciona un idioma", { description: "Com a professor, has d'indicar almenys un idioma." });
      return;
    }

    setIsLoading(true);

    try {
      // El backend només accepta username, email i password
      await api.post('/auth/register', {
        username,
        email,
        password
      });

      toast.success('Compte creat correctament!', {
        description: 'Ara ja pots iniciar sessió amb les teves dades.',
      });
      
      navigate('/login');
      
    } catch (error: any) {
      const errorMessage = error.response?.data?.error || 'S\'ha produït un error en el registre.';
      toast.error('Error de registre', { description: errorMessage });
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div className="min-h-screen flex items-center justify-center bg-slate-50 p-4 font-sans">
      <div className="w-full max-w-md">
        
        <button 
          onClick={() => navigate('/')}
          className="flex items-center text-sm font-medium text-slate-500 hover:text-slate-900 mb-6 transition-colors"
        >
          <ArrowLeft className="mr-2 h-4 w-4" />
          Tornar a l'inici
        </button>

        <Card className="p-8 border-slate-200 shadow-xl shadow-slate-200/50 bg-white">
          <div className="text-center mb-8">
            <h1 className="text-2xl font-bold text-slate-900 tracking-tight">Crea el teu compte</h1>
            <p className="text-slate-500 text-sm mt-2">Uneix-te a SmartNotes per revolucionar els teus apunts.</p>
          </div>

          <form onSubmit={handleRegister} className="space-y-6">
            
            {/* SELECCIÓ DE ROL */}
            <div className="space-y-3">
              <Label>Com faràs servir SmartNotes?</Label>
              <div className="grid grid-cols-2 gap-3">
                <Button
                  type="button"
                  variant={role === 'alumne' ? 'default' : 'outline'}
                  className={`h-24 flex flex-col gap-2 ${role === 'alumne' ? 'bg-blue-600 border-blue-600' : 'bg-white hover:bg-slate-50'}`}
                  onClick={() => setRole('alumne')}
                >
                  <BookOpen className={`h-6 w-6 ${role === 'alumne' ? 'text-white' : 'text-slate-500'}`} />
                  <span className={role === 'alumne' ? 'text-white' : 'text-slate-700'}>Soc Alumne</span>
                </Button>
                
                <Button
                  type="button"
                  variant={role === 'professor' ? 'default' : 'outline'}
                  className={`h-24 flex flex-col gap-2 ${role === 'professor' ? 'bg-blue-600 border-blue-600' : 'bg-white hover:bg-slate-50'}`}
                  onClick={() => setRole('professor')}
                >
                  <GraduationCap className={`h-6 w-6 ${role === 'professor' ? 'text-white' : 'text-slate-500'}`} />
                  <span className={role === 'professor' ? 'text-white' : 'text-slate-700'}>Soc Professor</span>
                </Button>
              </div>
            </div>

            {/* SELECCIÓ D'IDIOMES (Condicional) */}
            {role === 'professor' && (
              <div className="space-y-3 animate-in fade-in slide-in-from-top-2 duration-300">
                <Label>Quins idiomes imparteixes?</Label>
                <div className="flex flex-wrap gap-2">
                  {['Català', 'Castellà', 'Anglès', 'Francès'].map((lang) => {
                    const isSelected = languages.includes(lang);
                    return (
                      <Button
                        key={lang}
                        type="button"
                        variant={isSelected ? 'default' : 'outline'}
                        size="sm"
                        className={isSelected ? 'bg-slate-800' : 'bg-white'}
                        onClick={() => toggleLanguage(lang)}
                      >
                        {isSelected && <Check className="mr-1.5 h-3 w-3" />}
                        {lang}
                      </Button>
                    );
                  })}
                </div>
              </div>
            )}

            <div className="space-y-4 pt-2 border-t border-slate-100">
              <div className="space-y-2">
                <Label htmlFor="username">Nom d'usuari</Label>
                <Input 
                  id="username" 
                  placeholder="alumne2026 o profemates" 
                  required 
                  value={username}
                  onChange={(e) => setUsername(e.target.value)}
                />
              </div>

              <div className="space-y-2">
                <Label htmlFor="email">Correu electrònic</Label>
                <Input 
                  id="email" 
                  type="email" 
                  placeholder="correu@escola.cat" 
                  required 
                  value={email}
                  onChange={(e) => setEmail(e.target.value)}
                />
              </div>

              <div className="space-y-2">
                <Label htmlFor="password">Contrasenya</Label>
                <Input 
                  id="password" 
                  type="password" 
                  placeholder="Mínim 8 caràcters, 1 majúscula, 1 número..." 
                  required 
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                />
              </div>
            </div>

            <Button 
              type="submit" 
              className="w-full bg-blue-600 hover:bg-blue-700 text-base h-11 shadow-md shadow-blue-600/20" 
              disabled={isLoading}
            >
              {isLoading ? "Creant el compte..." : "Registrar-se"}
            </Button>
          </form>

          <div className="mt-6 text-center text-sm text-slate-500">
            Ja tens un compte?{' '}
            <button onClick={() => navigate('/login')} className="text-blue-600 hover:underline font-medium">
              Inicia sessió aquí
            </button>
          </div>
        </Card>
      </div>
    </div>
  );
}