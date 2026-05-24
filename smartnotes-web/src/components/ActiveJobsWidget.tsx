import { useState, useEffect } from 'react';
import { api } from '@/services/api';
import { Cpu, Loader2, CheckCircle2, AlertCircle, AudioLines, Languages, FileText, Sparkles } from 'lucide-react';

const statusIcons: Record<string, React.ElementType> = {
  AudioCleaning: AudioLines,
  DetectingLanguage: Languages,
  Transcribing: FileText,
  Summarizing: Sparkles,
  Done: CheckCircle2,
  Error: AlertCircle,
};

const statusColors: Record<string, string> = {
  AudioCleaning: 'text-orange-400',
  DetectingLanguage: 'text-yellow-400',
  Transcribing: 'text-blue-400',
  Summarizing: 'text-purple-400',
  Done: 'text-emerald-400',
  Error: 'text-red-400',
};

export default function ActiveJobsWidget() {
  const [activeJobs, setActiveJobs] = useState<any[]>([]);
  const [detailedJob, setDetailedJob] = useState<any | null>(null);

  useEffect(() => {
    let cancelled = false;

    const poll = async () => {
      try {
        const response = await api.get('/transcription/active');
        if (cancelled) return;
        const jobs = response.data || [];
        setActiveJobs(jobs);

        const pendingJobs = jobs.filter((j: any) => j.status !== 'Done' && j.status !== 'Error' && j.status !== 'Cancelled');
        if (pendingJobs.length > 0) {
          const statusResponse = await api.get(`/transcription/status/${pendingJobs[0].id}`);
          if (!cancelled) {
            setDetailedJob(statusResponse.data);
          }
        } else {
          setDetailedJob(null);
        }
      } catch (error) {
        console.error("Error polling jobs:", error);
      }
    };

    poll();
    const interval = setInterval(poll, 3000);
    return () => {
      cancelled = true;
      clearInterval(interval);
    };
  }, []);

  if (!detailedJob) return null;

  const status = detailedJob.status;
  const IconComponent = statusIcons[status] || Loader2;
  const iconColor = statusColors[status] || 'text-blue-400';
  const progress = detailedJob.progressPercentage || 0;
  const message = detailedJob.progressMessage || 'Processant...';
  const audioDuration = detailedJob.audioDuration || '';

  return (
    <div className="px-4 mb-4">
      <div className="bg-[#111827] rounded-xl p-4 shadow-lg border border-slate-800">
        <div className="flex items-center gap-2 mb-3">
          <Cpu className="h-4 w-4 text-blue-400" />
          <span className="text-[10px] font-bold tracking-wider text-slate-300 uppercase">
            {activeJobs.length > 1 ? `${activeJobs.length} Feines Actives` : 'IA Processant'}
          </span>
        </div>
        
        <div className="flex justify-between items-center mb-2">
          <span className="text-xs font-medium text-white truncate pr-2">
            {detailedJob.originalFileName || "Processant àudio..."}
          </span>
          <div className="flex items-center gap-1.5 shrink-0">
            <IconComponent className={`h-4 w-4 ${iconColor} ${status !== 'Done' ? 'animate-spin' : ''}`} />
            <span className="text-[10px] font-bold text-slate-400">{progress}%</span>
          </div>
        </div>

        {audioDuration && (
          <p className="text-[10px] text-slate-500 mb-2">Durada: {audioDuration}</p>
        )}
        
        <p className="text-[11px] text-slate-400 mb-2 truncate" title={message}>
          {message}
        </p>
        
        <div className="h-1.5 w-full bg-slate-800 rounded-full overflow-hidden">
          <div 
            className={`h-full rounded-full transition-all duration-500 ${
              status === 'Error' ? 'bg-red-500' : 
              status === 'Done' ? 'bg-emerald-500' : 
              'bg-blue-500'
            }`}
            style={{ width: `${progress}%` }} 
          />
        </div>
      </div>
    </div>
  );
}
