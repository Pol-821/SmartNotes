import { useState, useEffect } from 'react';
import { api } from '@/services/api';
import { Check, Loader2, CreditCard, Clock } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { FullPageLoader } from '@/components/ui/spinner';
import { toast } from 'sonner';
import { useNavigate } from 'react-router-dom';

interface Plan {
  id: number;
  name: string;
  description: string;
  priceMonthly: number;
  minutesPerMonth: number;
  secondsPerMonth: number;
}

interface MySubscription {
  hasSubscription: boolean;
  planName?: string;
  startDate?: string;
  nextBillingDate?: string;
  secondsPerMonth?: number;
}

export default function SubscriptionPage() {
  const navigate = useNavigate();
  const [plans, setPlans] = useState<Plan[]>([]);
  const [mySubscription, setMySubscription] = useState<MySubscription | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [isSubscribing, setIsSubscribing] = useState<number | null>(null);

  useEffect(() => {
    const fetchData = async () => {
      try {
        const [plansRes, subRes] = await Promise.all([
          api.get('/subscription/plans'),
          api.get('/subscription/my-subscription'),
        ]);
        setPlans(plansRes.data);
        setMySubscription(subRes.data);
      } catch (error) {
        console.error("Error carregant plans:", error);
        toast.error("No s'han pogut carregar els plans");
      } finally {
        setIsLoading(false);
      }
    };
    fetchData();
  }, []);

  const handleSubscribe = async (planId: number) => {
    setIsSubscribing(planId);
    try {
      const response = await api.post('/subscription/subscribe', { planId });
      toast.success(response.data.message);
      setMySubscription({
        hasSubscription: true,
        planName: plans.find(p => p.id === planId)?.name,
        nextBillingDate: response.data.nextBillingDate,
        secondsPerMonth: response.data.secondsAdded,
      });
    } catch (error: any) {
      console.error("Error subscrivint:", error);
      const msg = typeof error.response?.data === 'string' ? error.response.data : error.response?.data?.error || "Error en subscriure's";
      toast.error(msg);
    } finally {
      setIsSubscribing(null);
    }
  };

  const handleCancel = async () => {
    try {
      const response = await api.post('/subscription/cancel');
      toast.success(response.data.message);
      setMySubscription({ hasSubscription: false });
    } catch (error: any) {
      console.error("Error cancel·lant:", error);
      const msg = typeof error.response?.data === 'string' ? error.response.data : error.response?.data?.error || "Error en cancel·lar";
      toast.error(msg);
    }
  };

  if (isLoading) return <FullPageLoader text="Carregant plans..." />;

  return (
    <div className="max-w-5xl mx-auto">
      <div className="mb-8">
        <button onClick={() => navigate('/notes')} className="text-sm text-slate-500 hover:text-slate-900 mb-4 inline-flex items-center gap-1">
          ← Tornar
        </button>
        <h1 className="text-3xl font-bold text-slate-900">Plans de Subscripció</h1>
        <p className="text-slate-500 mt-1">Tria el pla que millor s'adapti a les teves necessitats.</p>
      </div>

      {mySubscription?.hasSubscription && (
        <div className="bg-emerald-50 border border-emerald-200 rounded-xl p-4 mb-8 flex items-center justify-between">
          <div>
            <p className="font-semibold text-emerald-800">Pla Actiu: {mySubscription.planName}</p>
            <p className="text-sm text-emerald-600">Pròxima facturació: {mySubscription.nextBillingDate ? new Date(mySubscription.nextBillingDate).toLocaleDateString('ca-ES') : 'N/A'}</p>
          </div>
          <Button variant="outline" onClick={handleCancel} className="text-red-600 border-red-200 hover:bg-red-50">
            Cancel·lar
          </Button>
        </div>
      )}

      <div className="grid md:grid-cols-3 gap-6">
        {plans.map((plan) => {
          const isCurrentPlan = mySubscription?.hasSubscription && mySubscription.planName === plan.name;
          return (
            <div key={plan.id} className={`bg-white rounded-2xl shadow-sm border-2 p-6 flex flex-col ${isCurrentPlan ? 'border-blue-500 ring-2 ring-blue-200' : 'border-slate-200'}`}>
              {isCurrentPlan && <div className="bg-blue-500 text-white text-xs font-bold px-3 py-1 rounded-full w-fit mb-3">PLA ACTUAL</div>}
              <h2 className="text-2xl font-bold text-slate-900">{plan.name}</h2>
              <div className="mt-4 mb-6">
                <span className="text-4xl font-extrabold text-slate-900">{plan.priceMonthly}€</span>
                <span className="text-slate-500">/mes</span>
              </div>
              <p className="text-sm text-slate-600 mb-6">{plan.description}</p>
              <div className="space-y-3 mb-8 flex-1">
                <div className="flex items-center gap-2 text-sm text-slate-700">
                  <Clock className="h-4 w-4 text-blue-500" />
                  <span>{plan.minutesPerMonth} minuts d'àudio/mes</span>
                </div>
                <div className="flex items-center gap-2 text-sm text-slate-700">
                  <Check className="h-4 w-4 text-emerald-500" />
                  <span>Transcripció amb Whisper</span>
                </div>
                <div className="flex items-center gap-2 text-sm text-slate-700">
                  <Check className="h-4 w-4 text-emerald-500" />
                  <span>Resums amb IA</span>
                </div>
              </div>
              <Button
                onClick={() => handleSubscribe(plan.id)}
                disabled={isSubscribing === plan.id || isCurrentPlan}
                className={`w-full ${isCurrentPlan ? 'bg-slate-200 text-slate-500' : 'bg-blue-600 hover:bg-blue-700'}`}
              >
                {isSubscribing === plan.id ? <Loader2 className="h-4 w-4 animate-spin" /> : isCurrentPlan ? 'Subscrit' : <><CreditCard className="mr-2 h-4 w-4" /> Subscriure'm</>}
              </Button>
            </div>
          );
        })}
      </div>
    </div>
  );
}
