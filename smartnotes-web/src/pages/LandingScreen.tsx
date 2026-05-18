import { useNavigate } from 'react-router-dom';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { BrainCircuit, Mic, Users, ArrowRight, CheckCircle, Sparkles } from 'lucide-react';

export default function LandingScreen() {
  const navigate = useNavigate();

  return (
    <div className="min-h-screen bg-slate-50 font-sans selection:bg-blue-100">
      
      {/* NAVEGACIÓ (Topbar) */}
      <nav className="max-w-7xl mx-auto px-6 py-4 flex items-center justify-between">
        <div className="flex items-center gap-2">
          <div className="bg-blue-600 p-2 rounded-lg">
            <BrainCircuit className="h-6 w-6 text-white" />
          </div>
          <span className="text-xl font-bold text-slate-900 tracking-tight">SmartNotes</span>
        </div>
        <div className="flex items-center gap-4">
          <Button variant="ghost" className="text-slate-600 hover:text-slate-900" onClick={() => navigate('/login')}>
            Inicia Sessió
          </Button>
          <Button className="bg-blue-600 hover:bg-blue-700 shadow-sm" onClick={() => navigate('/register')}>
            Comença de franc
          </Button>
        </div>
      </nav>

      {/* HERO SECTION (La part principal que es veu només entrar) */}
      <main className="max-w-7xl mx-auto px-6 pt-20 pb-24 text-center">
        <div className="inline-flex items-center gap-2 px-3 py-1 rounded-full bg-blue-50 text-blue-700 text-sm font-medium mb-8">
          <Sparkles className="h-4 w-4" />
          <span>La IA que transforma les teves classes</span>
        </div>
        
        <h1 className="text-5xl md:text-7xl font-extrabold text-slate-900 tracking-tight max-w-4xl mx-auto leading-tight mb-8">
          De la veu als apunts en <span className="text-transparent bg-clip-text bg-gradient-to-r from-blue-600 to-indigo-600">qüestió de segons.</span>
        </h1>
        
        <p className="text-xl text-slate-600 max-w-2xl mx-auto mb-10 leading-relaxed">
          Grava les teves lliçons i deixa que la nostra Intel·ligència Artificial generi apunts estructurats, resums i tasques automàticament. Comparteix-los a l'instant amb els teus alumnes.
        </p>
        
        <div className="flex flex-col sm:flex-row items-center justify-center gap-4">
          <Button size="lg" className="bg-blue-600 hover:bg-blue-700 text-lg px-8 h-14 w-full sm:w-auto shadow-lg shadow-blue-600/20" onClick={() => navigate('/register')}>
            Crea el teu compte
            <ArrowRight className="ml-2 h-5 w-5" />
          </Button>
          <Button size="lg" variant="outline" className="text-lg px-8 h-14 w-full sm:w-auto bg-white" onClick={() => navigate('/login')}>
            Soc un alumne
          </Button>
        </div>
      </main>

      {/* SECCIÓ DE CARACTERÍSTIQUES (Features) */}
      <section className="bg-white py-24 border-t border-slate-100">
        <div className="max-w-7xl mx-auto px-6">
          <div className="text-center mb-16">
            <h2 className="text-3xl font-bold text-slate-900 mb-4">Tot el que necessites per a la teva aula</h2>
            <p className="text-slate-500 max-w-2xl mx-auto text-lg">Hem dissenyat SmartNotes pensant específicament en les necessitats de professors i estudiants.</p>
          </div>

          <div className="grid md:grid-cols-3 gap-8">
            <Card className="p-8 border-slate-100 shadow-sm hover:shadow-md transition-shadow bg-slate-50/50">
              <div className="bg-blue-100 w-12 h-12 rounded-xl flex items-center justify-center mb-6">
                <Mic className="text-blue-600 h-6 w-6" />
              </div>
              <h3 className="text-xl font-bold text-slate-900 mb-3">Transcripció Perfecta</h3>
              <p className="text-slate-600 leading-relaxed">La nostra IA impulsada per Whisper entén el context educatiu, el vocabulari tècnic i múltiples idiomes a la perfecció.</p>
            </Card>

            <Card className="p-8 border-slate-100 shadow-sm hover:shadow-md transition-shadow bg-slate-50/50">
              <div className="bg-indigo-100 w-12 h-12 rounded-xl flex items-center justify-center mb-6">
                <BrainCircuit className="text-indigo-600 h-6 w-6" />
              </div>
              <h3 className="text-xl font-bold text-slate-900 mb-3">Apunts Intel·ligents</h3>
              <p className="text-slate-600 leading-relaxed">No només transcrivim. Llama estructura el text, n'extreu els conceptes clau i genera llistes de tasques automàticament.</p>
            </Card>

            <Card className="p-8 border-slate-100 shadow-sm hover:shadow-md transition-shadow bg-slate-50/50">
              <div className="bg-emerald-100 w-12 h-12 rounded-xl flex items-center justify-center mb-6">
                <Users className="text-emerald-600 h-6 w-6" />
              </div>
              <h3 className="text-xl font-bold text-slate-900 mb-3">Aules Virtuals</h3>
              <p className="text-slate-600 leading-relaxed">Crea grups de classe i comparteix els teus apunts amb els alumnes mitjançant un simple codi d'invitació.</p>
            </Card>
          </div>
        </div>
      </section>

      {/* PEU DE PÀGINA (Footer) */}
      <footer className="bg-slate-900 py-12 text-center text-slate-400">
        <p>© 2026 SmartNotes SaaS. Tots els drets reservats.</p>
      </footer>
    </div>
  );
}