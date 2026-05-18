import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { api } from '@/services/api';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Card } from '@/components/ui/card';
import { ArrowLeft, KeyRound, Mail } from 'lucide-react';
import { toast } from 'sonner';

export default function ForgotPasswordScreen() {
  const navigate = useNavigate();
  const [email, setEmail] = useState('');
  const [isLoading, setIsLoading] = useState(false);
  const [isSent, setIsSent] = useState(false); // Controlem si ja hem enviat el correu

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setIsLoading(true);

    try {
      await api.post('/auth/forgot-password', { email });
      
      // Simulem el temps de càrrega per ara
      await new Promise(resolve => setTimeout(resolve, 1500));
      
      setIsSent(true);
      toast.success('Correu enviat', {
        description: "Revisa la teva safata d'entrada (i el correu brossa).",
      });
      
    } catch (error: any) {
      toast.error('Error', {
        description: "No hem pogut processar la teva sol·licitud. Torna-ho a intentar.",
      });
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div className="min-h-screen flex items-center justify-center bg-slate-50 p-4 font-sans">
      <div className="w-full max-w-md">
        
        <button 
          onClick={() => navigate('/login')}
          className="flex items-center text-sm font-medium text-slate-500 hover:text-slate-900 mb-6 transition-colors"
        >
          <ArrowLeft className="mr-2 h-4 w-4" />
          Tornar a l'inici de sessió
        </button>

        <Card className="p-8 border-slate-200 shadow-xl shadow-slate-200/50 bg-white">
          <div className="flex justify-center mb-6">
            <div className="bg-blue-50 p-3 rounded-full text-blue-600">
              <KeyRound className="h-8 w-8" />
            </div>
          </div>
          
          <div className="text-center mb-8">
            <h1 className="text-2xl font-bold text-slate-900 tracking-tight">Recupera la contrasenya</h1>
            <p className="text-slate-500 text-sm mt-2">
              {isSent 
                ? "T'hem enviat un enllaç de recuperació al teu correu." 
                : "Introdueix el teu correu electrònic i t'enviarem un enllaç per crear una contrasenya nova."}
            </p>
          </div>

          {!isSent ? (
            <form onSubmit={handleSubmit} className="space-y-6">
              <div className="space-y-2">
                <Label htmlFor="email">Correu electrònic</Label>
                <div className="relative">
                  <Mail className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-slate-400" />
                  <Input 
                    id="email" 
                    type="email" 
                    placeholder="correu@escola.cat" 
                    className="pl-10"
                    required 
                    value={email}
                    onChange={(e) => setEmail(e.target.value)}
                  />
                </div>
              </div>

              <Button 
                type="submit" 
                className="w-full bg-blue-600 hover:bg-blue-700 text-base h-11 shadow-md shadow-blue-600/20" 
                disabled={isLoading}
              >
                {isLoading ? "Enviant enllaç..." : "Enviar enllaç de recuperació"}
              </Button>
            </form>
          ) : (
            <Button 
              variant="outline"
              className="w-full h-11" 
              onClick={() => navigate('/login')}
            >
              Tornar al Login
            </Button>
          )}
        </Card>
      </div>
    </div>
  );
}