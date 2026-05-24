import { useState, useEffect } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { api } from '@/services/api';
import { FileText, Search, Calendar, ChevronRight, FolderOpen, ArrowLeft, Trash2, Users, Copy, UserMinus } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { FullPageLoader } from '@/components/ui/spinner';
import { toast } from 'sonner';
import ConfirmDialog from '@/components/ConfirmDialog';
import type { Classroom, Note, Student } from '@/types/api';

export default function ClassroomScreen() {
  const { id } = useParams();
  const navigate = useNavigate();
  
  const [isLoading, setIsLoading] = useState(true);
  const [classroom, setClassroom] = useState<Classroom | null>(null);
  const [notes, setNotes] = useState<Note[]>([]);
  const [students, setStudents] = useState<Student[]>([]);
  
  const [searchQuery, setSearchQuery] = useState('');
  const [activeTab, setActiveTab] = useState<'notes' | 'students'>('notes');
  
  // ESTATS DE LES MODALS
  const [isDeleteDialogOpen, setIsDeleteDialogOpen] = useState(false);
  const [studentToKick, setStudentToKick] = useState<{id: number, name: string} | null>(null);

  // Carregar dades mestres
  useEffect(() => {
    const fetchClassroomData = async () => {
      try {
        setIsLoading(true);
        const classResponse = await api.get('/classroom');
        const currentClass = classResponse.data.find((c: any) => c.id === Number(id));
        if (!currentClass) {
          toast.error("Aquesta aula no existeix");
          navigate('/notes');
          return;
        }
        setClassroom(currentClass);

        const notesResponse = await api.get('/notes?page=1&pageSize=50');
        const allNotes = notesResponse.data.items || notesResponse.data.Items || [];
        setNotes(allNotes.filter((n: any) => n.classroomId === Number(id)));

        const studentsResponse = await api.get(`/classroom/${id}/students`);
        setStudents(studentsResponse.data);

      } catch (error) {
        console.error("Error carregant dades:", error);
        toast.error("Error carregant la classe");
      } finally {
        setIsLoading(false);
      }
    };

    fetchClassroomData();
  }, [id, navigate]);

  const executeDeleteClassroom = async () => {
    try {
      await api.delete(`/classroom/${id}`);
      toast.success("Aula eliminada");
      navigate('/notes'); 
    } catch (error) {
      toast.error("No s'ha pogut eliminar l'aula");
      setIsDeleteDialogOpen(false);
    }
  };

  // NOU FLUX D'EXPULSIÓ D'ALUMNES (SENSE WINDOW.CONFIRM)
  const handleKickStudentClick = (studentId: number, studentName: string) => {
    setStudentToKick({ id: studentId, name: studentName });
  };

  const executeKickStudent = async () => {
    if (!studentToKick) return;
    try {
      await api.delete(`/classroom/${id}/students/${studentToKick.id}`);
      toast.success(`${studentToKick.name} ha estat expulsat.`);
      setStudents(students.filter(s => s.id !== studentToKick.id));
    } catch (error) {
      toast.error("Error al expulsar l'alumne.");
    } finally {
      setStudentToKick(null); // Tanquem la modal
    }
  };

  const filteredNotes = notes.filter(note => 
    note.title?.toLowerCase().includes(searchQuery.toLowerCase())
  );

  if (isLoading) return <FullPageLoader text="Obrint la carpeta..." />;

  return (
    <div className="max-w-6xl mx-auto space-y-8 relative">
      
      {/* CAPÇALERA DE L'AULA */}
      <div className="flex flex-col md:flex-row md:items-center justify-between gap-6 border-b border-slate-200 pb-6">
        <div className="flex items-center gap-4">
          <Button variant="ghost" size="icon" onClick={() => navigate('/notes')} className="text-slate-500 rounded-full hover:bg-slate-100 shrink-0">
            <ArrowLeft className="h-5 w-5" />
          </Button>
          <div>
            <div className="flex items-center gap-2">
              <span className="w-3.5 h-3.5 rounded-full shadow-sm" style={{ backgroundColor: classroom?.color }}></span>
              <h1 className="text-3xl font-bold tracking-tight text-slate-900">{classroom?.name}</h1>
            </div>
            <p className="text-slate-500 mt-1">Tens {notes.length} apunts i {students.length} alumnes matriculats.</p>
          </div>
        </div>

        <div className="flex items-center gap-3">
          <div className="flex items-center bg-slate-100 border border-slate-200 rounded-lg p-1 pr-4 shadow-sm">
            <div className="bg-white p-2 rounded-md shadow-sm mr-3">
              <Users className="h-4 w-4 text-blue-600" />
            </div>
            <div className="flex flex-col">
              <span className="text-[10px] font-bold text-slate-500 uppercase tracking-wider leading-none mb-1">Codi per alumnes</span>
              <span className="text-sm font-mono font-bold text-slate-900 leading-none tracking-widest">{classroom?.code || "SENSE-CODI"}</span>
            </div>
            <Button variant="ghost" size="icon" className="ml-2 h-6 w-6 text-slate-400 hover:text-blue-600" onClick={() => { navigator.clipboard.writeText(classroom?.code || ""); toast.success("Codi copiat!"); }}>
              <Copy className="h-3 w-3" />
            </Button>
          </div>
          <Button variant="outline" onClick={() => setIsDeleteDialogOpen(true)} className="text-red-600 hover:bg-red-50 hover:text-red-700 border-red-200" title="Eliminar aquesta aula">
            <Trash2 className="h-4 w-4" />
          </Button>
        </div>
      </div>

      {/* PESTANYES (TABS) */}
      <div className="flex gap-4 border-b border-slate-200">
        <button 
          onClick={() => setActiveTab('notes')}
          className={`pb-3 text-sm font-semibold transition-colors relative ${activeTab === 'notes' ? 'text-blue-600' : 'text-slate-500 hover:text-slate-700'}`}
        >
          <div className="flex items-center gap-2">
            <FileText className="h-4 w-4" /> Apunts
          </div>
          {activeTab === 'notes' && <div className="absolute bottom-0 left-0 right-0 h-0.5 bg-blue-600 rounded-t-full"></div>}
        </button>
        <button 
          onClick={() => setActiveTab('students')}
          className={`pb-3 text-sm font-semibold transition-colors relative ${activeTab === 'students' ? 'text-blue-600' : 'text-slate-500 hover:text-slate-700'}`}
        >
          <div className="flex items-center gap-2">
            <Users className="h-4 w-4" /> Alumnes
          </div>
          {activeTab === 'students' && <div className="absolute bottom-0 left-0 right-0 h-0.5 bg-blue-600 rounded-t-full"></div>}
        </button>
      </div>

      {/* CONTINGUT: PESTANYA D'APUNTS */}
      {activeTab === 'notes' && (
        <div className="space-y-6 animate-in fade-in duration-300">
          {notes.length > 0 && (
            <div className="relative max-w-md">
              <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-slate-400" />
              <Input placeholder="Buscar en aquesta aula..." className="pl-10 bg-white border-slate-200" value={searchQuery} onChange={(e) => setSearchQuery(e.target.value)} />
            </div>
          )}

          {notes.length === 0 ? (
            <Card className="flex flex-col items-center justify-center py-24 text-center border-dashed border-2 bg-slate-50/50 shadow-none">
              <div className="bg-white p-4 rounded-full shadow-sm mb-4 border border-slate-100">
                <FolderOpen className="h-10 w-10" style={{ color: classroom?.color }} strokeWidth={1.5} />
              </div>
              <h3 className="text-xl font-semibold text-slate-900 mb-2">Aquesta aula està buida</h3>
              <p className="text-slate-500 max-w-sm mb-6">Encara no has mogut cap apunt aquí dins.</p>
            </Card>
          ) : (
            <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
              {filteredNotes.map((note) => (
                 <Card key={note.id} className="group cursor-pointer hover:border-blue-500 hover:shadow-md transition-all duration-200 bg-white flex flex-col" onClick={() => navigate(`/notes/${note.id}`)}>
                   <div className="p-6 space-y-4 flex-1">
                     <div className="flex justify-between items-start">
                       <div className="p-2 bg-blue-50 rounded-lg text-blue-600 group-hover:bg-blue-600 group-hover:text-white transition-colors">
                         <FileText className="h-6 w-6" />
                       </div>
                       <ChevronRight className="text-slate-300 group-hover:text-blue-500 transition-colors" />
                     </div>
                     <h3 className="font-semibold text-lg text-slate-900 line-clamp-2 leading-tight">{note.title || "Sense títol"}</h3>
                   </div>
                   <div className="px-6 py-4 border-t border-slate-100 mt-auto">
                     <div className="flex items-center text-sm text-slate-500">
                       <Calendar className="h-4 w-4 mr-1.5" />
                       <span>{note.createdAt ? new Date(note.createdAt).toLocaleDateString('ca-ES') : 'Avui'}</span>
                     </div>
                   </div>
                 </Card>
              ))}
            </div>
          )}
        </div>
      )}

      {/* CONTINGUT: PESTANYA D'ALUMNES */}
      {activeTab === 'students' && (
        <div className="space-y-4 animate-in fade-in duration-300 max-w-3xl">
          <h2 className="text-xl font-bold text-slate-900 mb-4">Llista de Classe</h2>
          
          {students.length === 0 ? (
            <Card className="flex flex-col items-center justify-center py-16 text-center border-dashed border-2 bg-slate-50/50 shadow-none">
              <Users className="h-12 w-12 text-slate-300 mb-3" />
              <h3 className="text-lg font-semibold text-slate-700">Cap alumne matriculat</h3>
              <p className="text-slate-500 text-sm mt-1">Comparteix el codi <strong>{classroom?.code}</strong> perquè s'apuntin.</p>
            </Card>
          ) : (
            <div className="bg-white border border-slate-200 rounded-xl overflow-hidden shadow-sm">
              <div className="grid grid-cols-1 divide-y divide-slate-100">
                {students.map((student) => (
                  <div key={student.id} className="flex items-center justify-between p-4 hover:bg-slate-50 transition-colors">
                    <div className="flex items-center gap-4">
                      <div className="h-10 w-10 bg-blue-100 text-blue-700 rounded-full flex items-center justify-center font-bold text-lg">
                        {student.username?.charAt(0).toUpperCase()}
                      </div>
                      <div>
                        <p className="font-semibold text-slate-900">{student.username}</p>
                        <p className="text-sm text-slate-500">{student.email}</p>
                      </div>
                    </div>
                    <Button 
                      variant="ghost" 
                      size="sm" 
                      onClick={() => handleKickStudentClick(student.id, student.username)}
                      className="text-slate-400 hover:text-amber-600 hover:bg-amber-50 transition-colors"
                      title="Expulsar de la classe"
                    >
                      <UserMinus className="h-4 w-4 mr-2" /> Expulsar
                    </Button>
                  </div>
                ))}
              </div>
            </div>
          )}
        </div>
      )}

      {/* MODAL ELIMINAR AULA */}
      <ConfirmDialog
        open={isDeleteDialogOpen}
        onOpenChange={setIsDeleteDialogOpen}
        title="Eliminar Aula"
        description={`Estàs segur que vols eliminar ${classroom?.name}? Els apunts tornaran a la safata principal.`}
        confirmLabel="Sí, eliminar-la"
        cancelLabel="Cancel·lar"
        variant="destructive"
        onConfirm={executeDeleteClassroom}
      />

      {/* MODAL EXPULSAR ALUMNE */}
      <ConfirmDialog
        open={studentToKick !== null}
        onOpenChange={(open) => { if (!open) setStudentToKick(null); }}
        title="Expulsar Alumne"
        description={`Estàs segur que vols expulsar a ${studentToKick?.name} d'aquesta assignatura? Deixarà de tenir accés als apunts i haurà de tornar a demanar-te el codi per entrar.`}
        confirmLabel="Sí, expulsar"
        cancelLabel="Cancel·lar"
        variant="default"
        onConfirm={executeKickStudent}
      />

    </div>
  );
}