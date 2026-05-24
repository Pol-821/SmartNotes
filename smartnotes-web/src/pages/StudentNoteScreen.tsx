import { useState, useEffect } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import ReactMarkdown from 'react-markdown';
import remarkGfm from 'remark-gfm';
import { ArrowLeft, Calendar, Play, Download, Loader2, BookOpen } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import AudioPlayer from '@/components/AudioPlayer';
import { api } from '@/services/api';
import { toast } from 'sonner';
import { usePDF } from 'react-to-pdf';

export default function StudentNoteScreen() {
  const navigate = useNavigate();
  const { id } = useParams();

  const [note, setNote] = useState<any>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [showPlayer, setShowPlayer] = useState(false);

  const audioApiUrl = `/notes/${id}/audio`;

  const { toPDF, targetRef } = usePDF({ 
    filename: note?.title ? `${note.title}.pdf` : 'Apunt.pdf' 
  });

  useEffect(() => {
    const fetchNoteData = async () => {
      try {
        setIsLoading(true);
        // Truquem al nou endpoint segur per a alumnes
        const response = await api.get(`/classroom/note/${id}`);
        setNote(response.data);
      } catch (error) {
        console.error("Error carregant l'apunt:", error);
        toast.error("No s'ha pogut carregar l'apunt o no hi tens accés");
        navigate('/student');
      } finally {
        setIsLoading(false);
      }
    };

    if (id) fetchNoteData();
  }, [id, navigate]);

  if (isLoading) {
    return (
      <div className="min-h-screen flex items-center justify-center bg-slate-50">
        <Loader2 className="h-8 w-8 text-blue-600 animate-spin" />
      </div>
    );
  }

  if (!note) return null;

  return (
    <div className="min-h-screen bg-slate-50 py-8 px-4 sm:px-8">
      <div className="max-w-4xl mx-auto">
        
        {/* BOTÓ DE TORNAR */}
        <button 
          onClick={() => navigate('/student')} 
          className="group flex items-center text-sm font-medium text-slate-500 hover:text-blue-600 mb-8 transition-colors"
        >
          <ArrowLeft className="mr-2 h-4 w-4 group-hover:-translate-x-1 transition-transform" />
          Tornar a l'aula
        </button>

        <div className="space-y-6 mb-8">
          {/* ETIQUETA ALUMNE */}
          <div className="inline-flex items-center px-2.5 py-1 rounded-full text-xs font-semibold bg-blue-100 text-blue-700">
            <BookOpen className="w-3 h-3 mr-1.5" />
            Mode Lectura (Alumne)
          </div>

          <h1 className="text-4xl font-extrabold tracking-tight text-slate-900">
            {note.title || "Apunt sense títol"}
          </h1>
          
          <div className="flex items-center text-sm text-slate-500">
            <Calendar className="h-4 w-4 mr-1.5" />
            <span>Publicat el {note.createdAt ? new Date(note.createdAt).toLocaleDateString('ca-ES') : 'Avui'}</span>
          </div>

          {/* ACCIONS DE L'ALUMNE (Només Escoltar i PDF) */}
          <div className="flex flex-wrap gap-3 pt-6 border-t border-slate-200">
            <Button 
              className="bg-blue-600 hover:bg-blue-700 shadow-sm"
              onClick={() => setShowPlayer(!showPlayer)}
            >
              <Play className={`mr-2 h-4 w-4 ${showPlayer ? 'fill-current' : ''}`} />
              {showPlayer ? 'Amagar Àudio' : 'Escoltar Explicació'}
            </Button>
            <Button variant="outline" className="bg-white" onClick={() => toPDF()}>
              <Download className="mr-2 h-4 w-4" /> PDF
            </Button>
          </div>

          {/* REPRODUCTOR */}
          {showPlayer && (
            <div className="animate-in fade-in slide-in-from-top-4 duration-300">
              <AudioPlayer audioUrl={audioApiUrl} />
            </div>
          )}
        </div>

        {/* CONTINGUT DE L'APUNT */}
        <Card className="p-8 sm:p-12 bg-white shadow-sm border-slate-200 min-h-[500px]">
          {/* AQUEST DIV ÉS EL QUE CAPTURARÀ EL PDF */}
          <div ref={targetRef} className="p-4 bg-white"> 
            <h2 className="text-2xl font-bold mb-6 text-slate-900 hidden print:block">{note?.title}</h2>
            <article className="prose prose-slate prose-headings:font-semibold prose-a:text-blue-600 max-w-none">
              {note.content ? (
                <ReactMarkdown remarkPlugins={[remarkGfm]}>
                  {note.content}
                </ReactMarkdown>
              ) : (
                <p className="text-slate-500 italic text-center py-10">Aquesta classe no té contingut transcrit encara.</p>
              )}
            </article>
          </div>
        </Card>
        
      </div>
    </div>
  );
}