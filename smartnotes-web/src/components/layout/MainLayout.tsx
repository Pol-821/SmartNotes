import type { ReactNode } from 'react';
import { useState, useEffect } from 'react';
import { useNavigate, useLocation } from 'react-router-dom';
import { BookOpen, Settings, LogOut, LayoutDashboard, Plus, Loader2, Cpu, CreditCard, Menu } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import ActiveJobsWidget from '@/components/ActiveJobsWidget';
import { api } from '@/services/api';
import { toast } from 'sonner';

interface MainLayoutProps {
  children: ReactNode;
}

// Llista de colors vius per a les noves carpetes
const FOLDER_COLORS = ['#ef4444', '#f97316', '#f59e0b', '#10b981', '#3b82f6', '#8b5cf6', '#ec4cc9'];

export default function MainLayout({ children }: MainLayoutProps) {
  const navigate = useNavigate();
  const location = useLocation();

  // Estats per a les Aules
  const [classrooms, setClassrooms] = useState<any[]>([]);
  const [isLoadingClassrooms, setIsLoadingClassrooms] = useState(true);
  const [isCreating, setIsCreating] = useState(false);
  const [newClassroomName, setNewClassroomName] = useState('');
  const [sidebarOpen, setSidebarOpen] = useState(false);
  const [userProfile, setUserProfile] = useState<any>(null);

  const handleLogout = () => {
    localStorage.removeItem('token');
    localStorage.removeItem('refreshToken');
    localStorage.removeItem('role');
    localStorage.removeItem('email');
    navigate('/login');
  };

  // 1. Carregar les Aules en iniciar
  const fetchClassrooms = async () => {
    try {
      const response = await api.get('/classroom');
      setClassrooms(response.data);
    } catch (error) {
      console.error("Error carregant les aules:", error);
    } finally {
      setIsLoadingClassrooms(false);
    }
  };

  useEffect(() => {
    fetchClassrooms();
  }, []);

  const fetchProfile = async () => {
    try {
      const response = await api.get('/user/me');
      setUserProfile(response.data);
    } catch {
      // Fallback: mantenir valors anteriors
    }
  };

  useEffect(() => {
    fetchProfile();
  }, []);

  // 2. Crear una Aula nova
  const handleCreateClassroom = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!newClassroomName.trim()) return;

    try {
      // Agafem un color aleatori de la nostra llista
      const randomColor = FOLDER_COLORS[Math.floor(Math.random() * FOLDER_COLORS.length)];
      
      const response = await api.post('/classroom', {
        name: newClassroomName,
        color: randomColor
      });

      // Afegim la nova aula a la llista actual i netegem el formulari
      setClassrooms([response.data, ...classrooms]);
      setNewClassroomName('');
      setIsCreating(false);
      toast.success("Aula creada correctament!");
    } catch (error) {
      console.error("Error creant l'aula:", error);
      toast.error("No s'ha pogut crear l'assignatura");
    }
  };

  const menuItems = [
    { icon: LayoutDashboard, label: 'Tots els Apunts', path: '/notes' },
    { icon: CreditCard, label: 'Subscripció', path: '/subscription' },
    { icon: Cpu, label: 'La meva Raspberry Pi', path: '/raspberry' },
    { icon: Settings, label: 'Configuració', path: '/settings' },
  ];

  return (
    <div className="min-h-screen bg-slate-50 flex">
      
      {/* Mobile sidebar overlay */}
      {sidebarOpen && (
        <div className="fixed inset-0 z-40 bg-black/50 md:hidden" onClick={() => setSidebarOpen(false)} />
      )}
      
      {/* Menú Lateral (Sidebar) */}
      <aside className={`w-64 bg-white border-r border-slate-200 flex flex-col fixed md:sticky top-0 h-screen z-50 transition-transform duration-300 ${sidebarOpen ? 'translate-x-0' : '-translate-x-full'} md:translate-x-0`}>
        {/* Logo */}
        <div className="h-16 flex items-center px-6 border-b border-slate-200">
          <BookOpen className="text-blue-600 mr-2" size={24} />
          <span className="font-bold text-xl tracking-tight text-slate-900">SmartNotes</span>
        </div>

        {/* Navegació Principal */}
        <nav className="py-6 px-4 space-y-1">
          {menuItems.map((item) => {
            const isActive = location.pathname === item.path;
            return (
              <button
                key={item.path}
                onClick={() => navigate(item.path)}
                className={`w-full flex items-center space-x-3 px-3 py-2 rounded-lg text-sm font-medium transition-colors ${
                  isActive 
                    ? 'bg-blue-50 text-blue-700' 
                    : 'text-slate-600 hover:bg-slate-100 hover:text-slate-900'
                }`}
              >
                <item.icon size={20} />
                <span>{item.label}</span>
              </button>
            );
          })}
        </nav>

        {/* Separador */}
        <div className="px-6 py-2">
          <div className="h-px w-full bg-slate-100"></div>
        </div>

        {/* SECCIÓ D'AULES / ASSIGNATURES */}
        <div className="flex-1 overflow-y-auto px-4 py-2">
          <div className="flex items-center justify-between px-3 mb-2">
            <span className="text-xs font-bold text-slate-400 uppercase tracking-wider">Les meves Aules</span>
            <button 
              onClick={() => setIsCreating(!isCreating)}
              className="text-slate-400 hover:text-blue-600 transition-colors"
            >
              <Plus size={16} />
            </button>
          </div>

          {/* Formulari ràpid per crear aula (només visible si es fa clic al +) */}
          {isCreating && (
            <form onSubmit={handleCreateClassroom} className="mb-3 px-2">
              <Input 
                autoFocus
                placeholder="Ex: Mates 3r ESO..." 
                className="h-8 text-sm bg-slate-50 border-slate-200"
                value={newClassroomName}
                onChange={(e) => setNewClassroomName(e.target.value)}
                onBlur={() => {
                  // Si fem clic fora i està buit, ho tanquem
                  if (!newClassroomName.trim()) setIsCreating(false);
                }}
              />
            </form>
          )}

          {/* Llista d'Aules */}
          {isLoadingClassrooms ? (
            <div className="flex justify-center py-4">
              <Loader2 className="h-4 w-4 text-slate-300 animate-spin" />
            </div>
          ) : classrooms.length === 0 && !isCreating ? (
            <div className="px-3 py-2 text-xs text-slate-400 italic">
              Encara no tens cap aula. Crea'n una!
            </div>
          ) : (
            <div className="space-y-1">
              {classrooms.map((classroom) => {
                // De moment navega a una URL fictícia, ho programarem en el proper pas
                const isActive = location.pathname === `/classrooms/${classroom.id}`;
                return (
                  <button
                    key={classroom.id}
                    onClick={() => navigate(`/classrooms/${classroom.id}`)}
                    className={`w-full flex items-center space-x-3 px-3 py-2 rounded-lg text-sm font-medium transition-colors ${
                      isActive 
                        ? 'bg-blue-50 text-slate-900' 
                        : 'text-slate-600 hover:bg-slate-50 hover:text-slate-900'
                    }`}
                  >
                    {/* Puntet de color per identificar l'assignatura */}
                    <span 
                      className="w-2.5 h-2.5 rounded-full" 
                      style={{ backgroundColor: classroom.color }}
                    ></span>
                    <span className="truncate">{classroom.name}</span>
                  </button>
                );
              })}
            </div>
          )}
        </div>

        {/* Giny de processament (El que vam fer abans) */}
        <ActiveJobsWidget />

        {/* Peus del menú (Usuari i Logout) */}
        <div className="p-4 border-t border-slate-200">
          <div className="flex items-center gap-3 px-3 py-2 mb-2">
            <div className="w-8 h-8 rounded-full bg-blue-100 flex items-center justify-center text-blue-700 font-bold text-sm">
              {userProfile?.username ? userProfile.username.charAt(0).toUpperCase() + (userProfile.username.charAt(1)?.toUpperCase() || '') : '?'}
            </div>
            <div className="flex flex-col text-left">
              <span className="text-sm font-semibold text-slate-900">{userProfile?.username || 'Usuari'}</span>
              <span className="text-xs text-slate-500">{userProfile?.role === 'alumne' ? 'Alumne' : 'Professor'}</span>
            </div>
          </div>
          <Button 
            variant="ghost" 
            className="w-full justify-start text-red-600 hover:text-red-700 hover:bg-red-50"
            onClick={handleLogout}
          >
            <LogOut className="mr-2 h-4 w-4" />
            Tancar Sessió
          </Button>
        </div>
      </aside>

      {/* Contingut Principal */}
      <main className="flex-1 flex flex-col h-screen overflow-hidden">
        <header className="h-16 bg-white border-b border-slate-200 flex items-center px-4 md:hidden">
          <button onClick={() => setSidebarOpen(true)} className="mr-3 text-slate-600">
            <Menu size={24} />
          </button>
          <BookOpen className="text-blue-600 mr-2" size={24} />
          <span className="font-bold text-lg text-slate-900">SmartNotes</span>
        </header>

        <div className="flex-1 overflow-y-auto p-8">
          {children}
        </div>
      </main>

    </div>
  );
}