import { FileText, Calendar, ChevronRight } from 'lucide-react';
import { Card } from '@/components/ui/card';
import type { Note } from '@/types/api';

interface NoteCardProps {
  note: Note;
  onClick: () => void;
}

export default function NoteCard({ note, onClick }: NoteCardProps) {
  return (
    <Card 
      className="group cursor-pointer hover:border-blue-500 hover:shadow-md transition-all duration-200 bg-white flex flex-col"
      onClick={onClick}
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
        <div className="flex items-center text-sm text-slate-500">
          <Calendar className="h-4 w-4 mr-1.5" />
          <span>{note.createdAt ? new Date(note.createdAt).toLocaleDateString('ca-ES') : 'Avui'}</span>
        </div>
      </div>
    </Card>
  );
}
