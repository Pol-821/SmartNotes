import { Loader2 } from "lucide-react";

interface SpinnerProps {
  size?: "sm" | "md" | "lg" | "xl";
  className?: string;
  text?: string;
}

export function Spinner({ size = "md", className = "", text }: SpinnerProps) {
  // Lògica per calcular la mida de la icona
  const sizeClasses = {
    sm: "h-4 w-4",
    md: "h-8 w-8",
    lg: "h-12 w-12",
    xl: "h-16 w-16",
  };

  return (
    <div className={`flex flex-col items-center justify-center gap-3 text-slate-500 ${className}`}>
      <Loader2 className={`animate-spin text-blue-600 ${sizeClasses[size]}`} />
      {text && <p className="text-sm font-medium animate-pulse">{text}</p>}
    </div>
  );
}

// Un embolcall per a quan volem que el Loader ocupi tota la pàgina sencera
export function FullPageLoader({ text = "Carregant..." }: { text?: string }) {
  return (
    <div className="h-[80vh] flex items-center justify-center">
      <Spinner size="lg" text={text} />
    </div>
  );
}