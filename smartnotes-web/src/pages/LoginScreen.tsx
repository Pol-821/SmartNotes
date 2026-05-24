import { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { BookOpen, ArrowRight } from 'lucide-react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Button } from "@/components/ui/button";
import { toast } from "sonner";
import { api } from '@/services/api';
import { getToken, isTokenExpired, clearAuth } from '@/lib/auth';

export default function LoginScreen() {
  const navigate = useNavigate();
  const [loginId, setLoginId] = useState('');
  const [password, setPassword] = useState('');
  const [isLoading, setIsLoading] = useState(false);

  useEffect(() => {
    const token = getToken();
    if (!token) return;
    if (isTokenExpired(token)) {
      clearAuth();
      return;
    }
    const role = localStorage.getItem('role');
    if (role === 'alumne') navigate('/student');
    else navigate('/notes');
  }, [navigate]);

  const handleLogin = async (e: React.FormEvent) => {
    e.preventDefault();
    setIsLoading(true);

    try {
      const response = await api.post('/auth/login', {
        username: loginId, 
        password: password
      });
      
      // EXTREIEM EL NOU 'role' QUE VE DEL BACKEND
      const { accessToken, refreshToken, role, email } = response.data;
      
      localStorage.setItem('token', accessToken);
      localStorage.setItem('refreshToken', refreshToken);
      localStorage.setItem('email', email);
      
      const userRole = role?.toLowerCase() || 'professor';
      localStorage.setItem('role', userRole);

      toast.success('Sessió iniciada correctament');
      
      // CONTROL DE TRÀNSIT: On enviem l'usuari?
      if (userRole === 'alumne') {
        navigate('/student');
      } else {
        navigate('/notes');
      }
      
    } catch (error: any) {
      const errorMessage = error.response?.data?.error || 'Credencials incorrectes';
      toast.error('Error de login', { description: errorMessage });
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div className="min-h-screen flex items-center justify-center bg-slate-50 p-4">
      <Card className="w-full max-w-md shadow-lg border-slate-200">
        <CardHeader className="space-y-2 text-center pb-6">
          <div className="flex justify-center mb-2">
            <div className="bg-slate-900 p-3 rounded-xl">
              <BookOpen size={32} className="text-white" />
            </div>
          </div>
          <CardTitle className="text-2xl font-bold tracking-tight">Benvingut a SmartNotes</CardTitle>
          <CardDescription className="text-slate-500">
            Introdueix les teves credencials per accedir.
          </CardDescription>
        </CardHeader>
        <CardContent>
          <form onSubmit={handleLogin} className="space-y-6">
            <div className="space-y-2">
              <Label htmlFor="loginId">Usuari o Correu Electrònic</Label>
              <Input 
                id="loginId" 
                type="text" 
                placeholder="profesor123 o profe@escola.cat" 
                value={loginId}
                onChange={(e) => setLoginId(e.target.value)}
                required 
              />
            </div>
            <div className="space-y-2">
              <div className="flex items-center justify-between">
                <Label htmlFor="password">Contrasenya</Label>
                <button 
                  type="button"
                  onClick={() => navigate('/forgot-password')}
                  className="text-sm font-medium text-blue-600 hover:underline"
                >
                  L'has oblidat?
                </button>
              </div>
              <Input 
                id="password" 
                type="password" 
                placeholder="••••••••" 
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                required 
              />
            </div>
            <Button type="submit" className="w-full" disabled={isLoading}>
              {isLoading ? 'Verificant...' : 'Iniciar Sessió'}
              {!isLoading && <ArrowRight className="ml-2 h-4 w-4" />}
            </Button>
          </form>
          <div className="mt-6 text-center text-sm text-slate-500">
            No tens un compte encara?{' '}
            <button 
              type="button" 
              onClick={() => navigate('/register')} 
              className="text-blue-600 hover:underline font-medium"
            >
              Registra't aquí
            </button>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}