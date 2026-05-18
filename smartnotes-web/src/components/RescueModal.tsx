import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription, DialogFooter } from "@/components/ui/dialog";
import { Button } from "@/components/ui/button";
import { AlertTriangle, RefreshCw } from "lucide-react";

interface RescueModalProps {
  isOpen: boolean;
  onClose: () => void;
  onRetry: () => void;
  fileName: string;
}

export default function RescueModal({ isOpen, onClose, onRetry, fileName }: RescueModalProps) {
  return (
    <Dialog open={isOpen} onOpenChange={onClose}>
      <DialogContent className="sm:max-w-md bg-white">
        <DialogHeader>
          <div className="flex items-center gap-3 mb-2">
            <div className="p-2 bg-red-50 text-red-600 rounded-full">
              <AlertTriangle size={24} strokeWidth={2} />
            </div>
            <DialogTitle className="text-xl font-bold text-slate-900">
              Error de processament
            </DialogTitle>
          </div>
          <DialogDescription className="text-slate-600 text-base">
            La IA no ha pogut completar la transcripció de l'arxiu <span className="font-semibold text-slate-900">{fileName}</span>. 
            Això sol passar per talls de connexió o si l'àudio té molt soroll de fons.
          </DialogDescription>
        </DialogHeader>
        
        <DialogFooter className="sm:justify-end gap-2 pt-6 border-t border-slate-100 mt-2">
          <Button type="button" variant="ghost" onClick={onClose} className="text-slate-600">
            Cancel·lar
          </Button>
          <Button 
            type="button" 
            className="bg-blue-600 hover:bg-blue-700 text-white" 
            onClick={() => {
              onRetry();
              onClose();
            }}
          >
            <RefreshCw className="mr-2 h-4 w-4" />
            Tornar a intentar
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}