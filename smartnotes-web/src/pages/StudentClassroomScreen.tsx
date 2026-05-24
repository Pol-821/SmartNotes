import { useState, useEffect } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { api } from '@/services/api';
import { FileText, Search, ArrowLeft, Folder } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { FullPageLoader } from '@/components/ui/spinner';
import { toast } from 'sonner';
import NoteCard from '@/components/NoteCard';
import type { Note, Classroom } from '@/types/api';

export default function StudentClassroomScreen() {
  const { id } = useParams();
  const navigate = useNavigate();
  const [isLoading, setIsLoading] = useState(true);
  const [notes, setNotes] = useState<Note[]>([]);
  const [classroom, setClassroom] = useState<Classroom | null>(null);
  const [searchQuery, setSearchQuery] = useState('');

  useEffect(() => {
    const fetchClassData = async () => {
      try {
        setIsLoading(true);
        
        // 1. Busquem les dades de l'aula a la meva llista de matrícules
        const enrolledResponse = await api.get('/classroom/enrolled');
        const currentClass = enrolledResponse.data.find((c: any) => c.id === Number(id));
        
        if (!currentClass) {
          toast.error("No tens accés a aquesta classe.");
          navigate('/student');
          return;
        }
        setClassroom(currentClass);

        // 2. Demanem els apunts mitjançant el nou endpoint que hem fet al C#
        const notesResponse = await api.get(`/classroom/${id}/notes`);
        setNotes(notesResponse.data);

      } catch (error) {
        console.error("Error carregant l'aula:", error);
        toast.error("No s'han pogut carregar els apunts");
        navigate('/student');
      } finally {
        setIsLoading(false);
      }
    };

    if (id) fetchClassData();
  }, [id, navigate]);

  const filteredNotes = notes.filter(note => 
    note.title?.toLowerCase().includes(searchQuery.toLowerCase())
  );

  if (isLoading) return <FullPageLoader text="Carregant aula..." />;

  return (
    <div className="min-h-screen bg-slate-50 flex flex-col">
      {/* CAPÇALERA LLEUGERA */}
      <header className="h-16 bg-white border-b border-slate-200 flex items-center px-8 shadow-sm shrink-0">
        <Button variant="ghost" size="sm" onClick={() => navigate('/student')} className="text-slate-500 hover:text-slate-900 mr-4">
          <ArrowLeft className="h-4 w-4 mr-2" />
          Tornar a les meves aules
        </Button>
      </header>

      <main className="flex-1 max-w-5xl mx-auto w-full p-8 space-y-8">
        
        {/* TÍTOL DE L'ASSIGNATURA */}
        <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 border-b border-slate-200 pb-6">
          <div className="flex items-center gap-4">
            <div className="p-3 rounded-xl bg-white shadow-sm border border-slate-100">
              <Folder className="h-8 w-8" style={{ color: classroom?.color }} />
            </div>
            <div>
              <h1 className="text-3xl font-bold tracking-tight text-slate-900">{classroom?.name}</h1>
              <p className="text-slate-500 mt-1">El teu professor ha pujat {notes.length} apunts.</p>
            </div>
          </div>
        </div>

        {/* BUSCADOR */}
        {notes.length > 0 && (
          <div className="relative max-w-md">
            <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-slate-400" />
            <Input 
              placeholder="Buscar en aquesta aula..." 
              className="pl-10 bg-white border-slate-200 shadow-sm"
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
            />
          </div>
        )}

        {/* LLISTA D'APUNTS */}
        {notes.length === 0 ? (
          <div className="text-center py-20 bg-white rounded-3xl border border-slate-200 shadow-sm">
            <FileText className="h-12 w-12 text-slate-300 mx-auto mb-4" />
            <p className="text-slate-500 font-medium text-lg">L'aula està buida</p>
            <p className="text-slate-400 mt-1">El teu professor encara no ha publicat cap apunt aquí.</p>
          </div>
        ) : (
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
            {filteredNotes.map((note) => (
              <NoteCard key={note.id} note={note} onClick={() => navigate(`/student/note/${note.id}`)} />
            ))}
          </div>
        )}
      </main>
    </div>
  );
}