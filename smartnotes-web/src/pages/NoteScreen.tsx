import { useState, useEffect, useRef } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import ReactMarkdown from 'react-markdown';
import remarkGfm from 'remark-gfm';
import { ArrowLeft, Calendar, Clock, Download, Play, FolderOpen, Trash2, AlertTriangle } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import AudioPlayer from '@/components/AudioPlayer';
import { FullPageLoader } from '@/components/ui/spinner';
import { api } from '@/services/api';
import { toast } from 'sonner';
import { jsPDF } from 'jspdf';
import html2canvas from 'html2canvas';

export default function NoteScreen() {
  const navigate = useNavigate();
  const { id } = useParams();

  const [note, setNote] = useState<any>(null);
  const [classrooms, setClassrooms] = useState<any[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [showPlayer, setShowPlayer] = useState(false);
  const [isDeleteDialogOpen, setIsDeleteDialogOpen] = useState(false);
  const [isGeneratingPDF, setIsGeneratingPDF] = useState(false);

  const audioApiUrl = `/notes/${id}/audio`;
  const pdfCaptureRef = useRef<HTMLDivElement>(null);

  const cleanMarkdownContent = (content: string): string => {
    const lines = content.split('\n');
    const result: string[] = [];
    let skipSection = false;

    const skipKeywords = /#{1,6}\s*(?:Propostes\s*d['']?estudi|Propostes\s*de\s*estudi|Study\s*Proposals|Tasques?\s*del\s*(?:professor|profe)|Teacher['']s?\s*Tasks|Tareas?\s*del\s*profesor)/i;

    for (const line of lines) {
      if (line.match(/^[\s]*[-*]\s+\[[ xX]\]/)) continue;

      if (skipKeywords.test(line)) {
        skipSection = true;
        continue;
      }

      if (skipSection && line.match(/^#{1,6}\s/)) {
        skipSection = false;
        result.push(line);
        continue;
      }

      if (!skipSection) {
        result.push(line);
      }
    }

    return result.join('\n').trim();
  };

  const handleDownloadPDF = async () => {
    if (!pdfCaptureRef.current || !note) return;
    setIsGeneratingPDF(true);

    await new Promise(resolve => setTimeout(resolve, 300));

    try {
      const canvas = await html2canvas(pdfCaptureRef.current!, {
        scale: 2,
        useCORS: true,
        backgroundColor: '#ffffff',
        logging: false,
        windowWidth: 750,
      });

      const pdf = new jsPDF('p', 'mm', 'a4');
      const pdfWidth = pdf.internal.pageSize.getWidth();
      const pdfHeight = pdf.internal.pageSize.getHeight();
      const margin = 20;
      const contentWidth = pdfWidth - margin * 2;
      const usableHeight = pdfHeight - margin * 2;

      const imgWidth = canvas.width;
      const imgHeight = canvas.height;
      const ratio = contentWidth / imgWidth;
      const scaledHeight = imgHeight * ratio;

      let heightLeft = scaledHeight;
      let position = margin;
      const imgData = canvas.toDataURL('image/png');

      pdf.addImage(imgData, 'PNG', margin, position, contentWidth, scaledHeight);
      heightLeft -= usableHeight;

      while (heightLeft > 0) {
        position = margin - (scaledHeight - heightLeft);
        pdf.addPage();
        pdf.addImage(imgData, 'PNG', margin, position, contentWidth, scaledHeight);
        heightLeft -= usableHeight;
      }

      pdf.save(`${note.title || 'Apunt'}.pdf`);
      toast.success('PDF descarregat correctament');
    } catch (error) {
      console.error('Error generant PDF:', error);
      toast.error('Error generant el PDF');
    } finally {
      setIsGeneratingPDF(false);
    }
  };

  useEffect(() => {
    const fetchInitialData = async () => {
      try {
        setIsLoading(true);
        const noteResponse = await api.get(`/notes/${id}`);
        setNote(noteResponse.data);

        const classResponse = await api.get('/classroom');
        setClassrooms(classResponse.data);
      } catch (error) {
        console.error("Error carregant les dades:", error);
        toast.error("No s'ha pogut carregar aquest apunt");
        navigate('/notes');
      } finally {
        setIsLoading(false);
      }
    };

    if (id) fetchInitialData();
  }, [id, navigate]);

  const handleMoveNote = async (classroomId: string) => {
    try {
      const parsedId = classroomId === "none" ? null : parseInt(classroomId);
      await api.put(`/notes/${id}/move`, { classroomId: parsedId });
      setNote({ ...note, classroomId: parsedId });
      toast.success("Apunt mogut d'aula correctament!");
    } catch (error) {
      console.error("Error movent la nota:", error);
      toast.error("No s'ha pogut moure la nota");
    }
  };

  const executeDeleteNote = async () => {
    try {
      await api.delete(`/notes/${id}`);
      toast.success("Apunt eliminat correctament");
      navigate('/notes');
    } catch (error) {
      console.error("Error esborrant la nota:", error);
      toast.error("No s'ha pogut esborrar l'apunt");
      setIsDeleteDialogOpen(false);
    }
  };

  if (isLoading) return <FullPageLoader text="Carregant els apunts..." />;
  if (!note) return null;

  const currentClassroom = classrooms.find(c => c.id === note.classroomId);

  return (
    <div className="max-w-4xl mx-auto pb-12 relative">
      
      <button 
        onClick={() => navigate(-1)} 
        className="group flex items-center text-sm font-medium text-slate-500 hover:text-slate-900 mb-8 transition-colors"
      >
        <ArrowLeft className="mr-2 h-4 w-4 group-hover:-translate-x-1 transition-transform" />
        Tornar enrere
      </button>

      <div className="space-y-6 mb-8">
        {currentClassroom && (
          <div className="inline-flex items-center px-2.5 py-1 rounded-full text-xs font-semibold bg-slate-100 text-slate-700">
            <span className="w-2 h-2 rounded-full mr-2" style={{ backgroundColor: currentClassroom.color }}></span>
            Pertany a: {currentClassroom.name}
          </div>
        )}

        <h1 className="text-4xl font-extrabold tracking-tight text-slate-900">
          {note.title || "Apunt sense títol"}
        </h1>
        
        <div className="flex flex-wrap items-center gap-4 text-sm text-slate-500">
          <div className="flex items-center gap-1.5">
            <Calendar className="h-4 w-4" />
            <span>{note.createdAt ? new Date(note.createdAt).toLocaleDateString('ca-ES') : 'Avui'}</span>
          </div>
          <div className="flex items-center gap-1.5">
            <Clock className="h-4 w-4" />
            <span>Àudio processat</span>
          </div>
        </div>

        <div className="flex flex-col sm:flex-row gap-4 pt-4 border-t border-slate-200 justify-between items-start sm:items-center">
          <div className="flex items-center gap-3">
            <Button 
              className="bg-blue-600 hover:bg-blue-700"
              onClick={() => setShowPlayer(!showPlayer)}
            >
              <Play className={`mr-2 h-4 w-4 ${showPlayer ? 'fill-current' : ''}`} />
              {showPlayer ? 'Amagar Reproductor' : 'Escoltar Àudio Net'}
            </Button>
            <Button variant="outline" className="bg-white" onClick={handleDownloadPDF} disabled={isGeneratingPDF}>
              <Download className="mr-2 h-4 w-4" /> {isGeneratingPDF ? 'Generant...' : 'PDF'}
            </Button>
          </div>

          <div className="flex items-center gap-3 w-full sm:w-auto">
            <div className="flex items-center flex-1 sm:flex-none gap-2 bg-white border border-slate-200 rounded-md px-3 py-1 shadow-sm">
              <FolderOpen className="h-4 w-4 text-slate-400" />
              <select 
                className="bg-transparent text-sm font-medium text-slate-700 focus:outline-none py-1.5 cursor-pointer"
                value={note.classroomId || "none"}
                onChange={(e) => handleMoveNote(e.target.value)}
              >
                <option value="none">Sense classificar...</option>
                {classrooms.map(c => (
                  <option key={c.id} value={c.id}>{c.name}</option>
                ))}
              </select>
            </div>

            <Button 
              variant="outline" 
              onClick={() => setIsDeleteDialogOpen(true)}
              className="text-red-600 hover:bg-red-50 hover:text-red-700 border-red-200"
              title="Esborrar aquest apunt"
            >
              <Trash2 className="h-4 w-4" />
            </Button>
          </div>
        </div>

        {showPlayer && (
          <div className="animate-in fade-in slide-in-from-top-4 duration-300">
            <AudioPlayer audioUrl={audioApiUrl} />
          </div>
        )}
      </div>

      <Card className="p-8 sm:p-12 bg-white shadow-sm border-slate-200 min-h-[500px]">
        <div className="p-4 bg-white min-h-[300px]" style={{ fontFamily: 'sans-serif', lineHeight: '1.6' }}> 
          <h2 className="text-2xl font-bold mb-6 text-slate-900">{note?.title}</h2>
          <article className="prose prose-slate prose-headings:font-semibold prose-a:text-blue-600 max-w-none" style={{ color: '#1e293b' }}>
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

      {/* Hidden div for PDF capture */}
      {note.content && (
        <div ref={pdfCaptureRef} style={{ position: 'absolute', left: '-9999px', top: 0, width: '750px', padding: '40px', background: '#ffffff', zIndex: -1 }}>
          <h1 style={{ fontSize: '22px', fontWeight: '700', color: '#0f172a', marginBottom: '20px', paddingBottom: '10px', borderBottom: '2px solid #e2e8f0', fontFamily: 'system-ui, sans-serif' }}>{note.title || 'Apunt sense títol'}</h1>
          <div style={{ fontFamily: 'system-ui, sans-serif', fontSize: '12px', lineHeight: '1.6', color: '#1e293b' }}>
            <ReactMarkdown remarkPlugins={[remarkGfm]}>
              {cleanMarkdownContent(note.content)}
            </ReactMarkdown>
          </div>
        </div>
      )}

      {/* MODAL PERSONALITZADA PER ELIMINAR APUNT */}
      {isDeleteDialogOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/40 backdrop-blur-sm animate-in fade-in duration-200 p-4">
          <div className="bg-white rounded-xl shadow-xl w-full max-w-md p-6 animate-in zoom-in-95 duration-200">
            <div className="flex items-center gap-3 mb-4">
              <div className="bg-red-100 p-2 rounded-full text-red-600">
                <AlertTriangle className="h-6 w-6" />
              </div>
              <h3 className="text-xl font-bold text-slate-900">Eliminar Apunt</h3>
            </div>
            
            <p className="text-slate-600 text-sm mb-6 leading-relaxed">
              Estàs segur que vols eliminar definitivament l'apunt <strong>{note.title}</strong>? Aquesta acció no es pot desfer i perdràs la transcripció i el resum generat.
            </p>
            
            <div className="flex justify-end gap-3">
              <Button 
                variant="outline" 
                onClick={() => setIsDeleteDialogOpen(false)}
                className="bg-white"
              >
                Cancel·lar
              </Button>
              <Button 
                className="bg-red-600 hover:bg-red-700 text-white shadow-md shadow-red-600/20 font-semibold" 
                onClick={executeDeleteNote}
              >
                Sí, eliminar-lo
              </Button>
            </div>
          </div>
        </div>
      )}
      
    </div>
  );
}
