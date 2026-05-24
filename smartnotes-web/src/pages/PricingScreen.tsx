import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { Check, Sparkles, ArrowLeft, Zap, Building2 } from 'lucide-react';

interface Plan {
  name: string;
  description: string;
  priceMonthly: string;
  priceAnnual: string;
  icon: React.ReactNode;
  features: string[];
  buttonText: string;
  buttonVariant: "default" | "outline" | "ghost" | "secondary" | "destructive" | "link";
  popular: boolean;
}

export default function PricingScreen() {
  const navigate = useNavigate();
  const [isAnnual, setIsAnnual] = useState(false);

  const plans: Plan[] = [
    {
      name: "Bàsic",
      description: "Perfecte per a alumnes i proves puntuals.",
      priceMonthly: "0",
      priceAnnual: "0",
      icon: <Sparkles className="h-6 w-6 text-slate-400" />,
      features: [
        "60 minuts de transcripció al mes",
        "Apunts bàsics estructurats",
        "Unir-se a grups il·limitats",
        "Suport de la comunitat"
      ],
      buttonText: "Pla Actual",
      buttonVariant: "outline",
      popular: false
    },
    {
      name: "Pro",
      description: "Per a professors que volen digitalitzar el seu dia a dia.",
      priceMonthly: "9,99",
      priceAnnual: "7,99",
      icon: <Zap className="h-6 w-6 text-blue-500" />,
      features: [
        "500 minuts de transcripció al mes",
        "Resums intel·ligents i llistes de tasques",
        "Creació de fins a 5 Aules Virtuals",
        "Sense filigrana als PDFs",
        "Suport prioritari per correu"
      ],
      buttonText: "Començar prova de 7 dies",
      buttonVariant: "default",
      popular: true
    },
    {
      name: "Centre Educatiu",
      description: "Llicència completa per a departaments i escoles.",
      priceMonthly: "29,99",
      priceAnnual: "24,99",
      icon: <Building2 className="h-6 w-6 text-indigo-500" />,
      features: [
        "2000 minuts de transcripció al mes",
        "Totes les funcions d'IA desbloquejades",
        "Aules Virtuals il·limitades",
        "Panell de control per a l'escola",
        "Assessorament personalitzat"
      ],
      buttonText: "Contactar amb vendes",
      buttonVariant: "outline",
      popular: false
    }
  ];

  return (
    <div className="min-h-screen bg-slate-50 py-12 px-4 font-sans">
      <div className="max-w-6xl mx-auto">
        
        <button 
          onClick={() => navigate('/notes')}
          className="flex items-center text-sm font-medium text-slate-500 hover:text-slate-900 mb-8 transition-colors"
        >
          <ArrowLeft className="mr-2 h-4 w-4" />
          Tornar al Dashboard
        </button>

        <div className="text-center mb-16">
          <h1 className="text-4xl font-extrabold text-slate-900 tracking-tight mb-4">
            Plans adaptats al teu ritme de classe
          </h1>
          <p className="text-lg text-slate-600 max-w-2xl mx-auto">
            Escull el pla que millor s'adapti a les teves necessitats. Pots canviar o cancel·lar la teva subscripció en qualsevol moment.
          </p>
          
          {/* TOGGLE MENSUAL / ANUAL */}
          <div className="flex items-center justify-center gap-3 mt-8">
            <span className={`text-sm font-medium ${!isAnnual ? 'text-slate-900' : 'text-slate-500'}`}>Mensual</span>
            <button 
              onClick={() => setIsAnnual(!isAnnual)}
              className="relative inline-flex h-6 w-11 items-center rounded-full bg-blue-600 transition-colors focus:outline-none"
            >
              <span className={`inline-block h-4 w-4 transform rounded-full bg-white transition-transform ${isAnnual ? 'translate-x-6' : 'translate-x-1'}`} />
            </button>
            <span className={`text-sm font-medium ${isAnnual ? 'text-slate-900' : 'text-slate-500'}`}>
              Anual <span className="ml-1.5 inline-flex items-center rounded-md bg-green-50 px-2 py-1 text-xs font-medium text-green-700 ring-1 ring-inset ring-green-600/20">Estalvia un 20%</span>
            </span>
          </div>
        </div>

        {/* GRAELLA DE PREUS */}
        <div className="grid md:grid-cols-3 gap-8 max-w-5xl mx-auto">
          {plans.map((plan) => (
            <Card 
              key={plan.name} 
              className={`relative p-8 flex flex-col ${plan.popular ? 'border-blue-600 shadow-xl shadow-blue-900/5 ring-1 ring-blue-600' : 'border-slate-200 shadow-sm'}`}
            >
              {plan.popular && (
                <div className="absolute -top-4 left-0 right-0 flex justify-center">
                  <span className="bg-blue-600 text-white text-xs font-bold px-3 py-1 rounded-full uppercase tracking-wider">
                    Més escollit
                  </span>
                </div>
              )}
              
              <div className="mb-6">
                <div className="flex items-center justify-between mb-2">
                  <h3 className="text-xl font-bold text-slate-900">{plan.name}</h3>
                  {plan.icon}
                </div>
                <p className="text-sm text-slate-500 min-h-[40px]">{plan.description}</p>
              </div>

              <div className="mb-6">
                <div className="flex items-baseline text-5xl font-extrabold text-slate-900">
                  €{isAnnual ? plan.priceAnnual : plan.priceMonthly}
                  <span className="ml-1 text-xl font-medium text-slate-500">/mes</span>
                </div>
                {isAnnual && plan.priceMonthly !== "0" && (
                  <p className="text-sm text-slate-500 mt-2">Facturat anualment</p>
                )}
              </div>

              <ul className="space-y-4 mb-8 flex-1">
                {plan.features.map((feature, idx) => (
                  <li key={idx} className="flex items-start">
                    <Check className="h-5 w-5 text-blue-600 shrink-0 mr-3" />
                    <span className="text-slate-600 text-sm">{feature}</span>
                  </li>
                ))}
              </ul>

              <Button 
                variant={plan.buttonVariant} 
                className={`w-full h-12 text-base ${plan.popular ? 'bg-blue-600 hover:bg-blue-700 shadow-md' : 'bg-white'}`}
                onClick={() => {
                  if (plan.name === "Bàsic") navigate('/notes');
                  // Aquí anirà la lògica de Stripe en un futur
                  else alert(`Iniciant procés de compra per al pla ${plan.name}...`); 
                }}
              >
                {plan.buttonText}
              </Button>
            </Card>
          ))}
        </div>
      </div>
    </div>
  );
}