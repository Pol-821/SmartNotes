import { useState, useRef, useEffect, useCallback } from 'react';
import { Play, Pause, Volume2, VolumeX, Loader2, AlertCircle } from 'lucide-react';
import { api } from '@/services/api';

interface AudioPlayerProps {
  audioUrl: string;
}

export default function AudioPlayer({ audioUrl }: AudioPlayerProps) {
  const audioRef = useRef<HTMLAudioElement>(null);
  const [isPlaying, setIsPlaying] = useState(false);
  const [progress, setProgress] = useState(0);
  const [currentTime, setCurrentTime] = useState(0);
  const [duration, setDuration] = useState(0);
  const [isMuted, setIsMuted] = useState(false);
  
  const [audioSource, setAudioSource] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const abortController = new AbortController();
    let blobUrl: string | null = null;

    const fetchAudio = async () => {
      try {
        setIsLoading(true);
        setError(null);
        
        const response = await api.get(audioUrl, { 
          responseType: 'blob',
          signal: abortController.signal 
        });
        
        blobUrl = URL.createObjectURL(response.data);
        if (!abortController.signal.aborted) {
          setAudioSource(blobUrl);
        }
      } catch (err: any) {
        if (abortController.signal.aborted) return;
        console.error("Error carregant l'àudio:", err);
        setError("No s'ha pogut carregar l'arxiu d'àudio.");
      } finally {
        if (!abortController.signal.aborted) {
          setIsLoading(false);
        }
      }
    };

    fetchAudio();

    return () => {
      abortController.abort();
      if (blobUrl) URL.revokeObjectURL(blobUrl);
    };
  }, [audioUrl]);

  const togglePlay = useCallback(() => {
    if (!audioRef.current) return;
    if (audioRef.current.paused) {
      audioRef.current.play().catch(() => {
        setError("No s'ha pogut reproduir l'àudio.");
      });
      setIsPlaying(true);
    } else {
      audioRef.current.pause();
      setIsPlaying(false);
    }
  }, []);

  const toggleMute = useCallback(() => {
    if (!audioRef.current) return;
    audioRef.current.muted = !audioRef.current.muted;
    setIsMuted(prev => !prev);
  }, []);

  const handleTimeUpdate = () => {
    if (!audioRef.current) return;
    const current = audioRef.current.currentTime;
    const total = audioRef.current.duration;
    setCurrentTime(current);
    setProgress((current / total) * 100);
  };

  const handleLoadedMetadata = () => {
    if (audioRef.current) {
      setDuration(audioRef.current.duration);
    }
  };

  const handleSeek = (e: React.ChangeEvent<HTMLInputElement>) => {
    if (!audioRef.current || duration <= 0) return;
    const newTime = (Number(e.target.value) / 100) * duration;
    audioRef.current.currentTime = newTime;
    setProgress(Number(e.target.value));
  };

  const formatTime = (time: number) => {
    if (isNaN(time)) return "00:00";
    const mins = Math.floor(time / 60);
    const secs = Math.floor(time % 60);
    return `${mins.toString().padStart(2, '0')}:${secs.toString().padStart(2, '0')}`;
  };

  // 3. RENDERITZAT (ESTATS VISUALS)
  if (isLoading) {
    return (
      <div className="bg-slate-900 rounded-2xl p-4 flex items-center justify-center gap-3 text-slate-400">
        <Loader2 className="h-5 w-5 animate-spin" />
        <span className="text-sm font-medium">Carregant la gravació segura...</span>
      </div>
    );
  }

  if (error || !audioSource) {
    return (
      <div className="bg-red-50 border border-red-200 rounded-2xl p-4 flex items-center gap-3 text-red-600">
        <AlertCircle className="h-5 w-5" />
        <span className="text-sm font-medium">{error || "Error de càrrega"}</span>
      </div>
    );
  }

  return (
    <div className="bg-slate-900 rounded-2xl p-4 shadow-lg shadow-slate-900/10">
      <audio 
        ref={audioRef} 
        src={audioSource} 
        onTimeUpdate={handleTimeUpdate}
        onLoadedMetadata={handleLoadedMetadata}
        onEnded={() => setIsPlaying(false)}
        onError={() => setError("Error reproduint l'àudio.")}
      />
      
      <div className="flex items-center gap-4">
        {/* Play/Pause Button */}
        <button 
          onClick={togglePlay}
          className="w-12 h-12 shrink-0 bg-blue-600 hover:bg-blue-500 rounded-full flex items-center justify-center text-white transition-colors shadow-sm"
        >
          {isPlaying ? <Pause className="h-5 w-5" /> : <Play className="h-5 w-5 ml-1" />}
        </button>

        {/* Progress Bar & Timers */}
        <div className="flex-1 space-y-2">
          <div className="flex items-center justify-between text-xs font-medium text-slate-400">
            <span>{formatTime(currentTime)}</span>
            <span>{formatTime(duration)}</span>
          </div>
          
          <div className="relative group">
            <input 
              type="range" 
              min="0" 
              max="100" 
              value={progress || 0} 
              onChange={handleSeek}
              className="absolute inset-0 w-full h-full opacity-0 cursor-pointer z-10"
            />
            {/* Custom Track */}
            <div className="h-2 w-full bg-slate-800 rounded-full overflow-hidden">
              <div 
                className="h-full bg-blue-500 transition-all duration-100 ease-linear"
                style={{ width: `${progress}%` }}
              />
            </div>
          </div>
        </div>

        {/* Mute Button */}
        <button 
          onClick={toggleMute}
          className="shrink-0 text-slate-400 hover:text-white transition-colors p-2"
        >
          {isMuted ? <VolumeX className="h-5 w-5" /> : <Volume2 className="h-5 w-5" />}
        </button>
      </div>
    </div>
  );
}