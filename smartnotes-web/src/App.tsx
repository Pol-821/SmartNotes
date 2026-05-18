import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import LandingScreen from './pages/LandingScreen';
import LoginScreen from './pages/LoginScreen';
import MainLayout from './components/layout/MainLayout'; // <-- Importem el Layout
import DashboardScreen from './pages/DashboardScreen';
import RegisterScreen from './pages/RegisterScreen';
import ForgotPasswordScreen from './pages/ForgotPasswordScreen';
import PricingScreen from './pages/PricingScreen';
import { Toaster } from "@/components/ui/sonner";
import NoteScreen from './pages/NoteScreen';
import ClassroomScreen from '@/pages/ClassroomScreen';
import StudentDashboard from '@/pages/StudentDashboard';
import StudentClassroomScreen from '@/pages/StudentClassroomScreen';
import StudentNoteScreen from '@/pages/StudentNoteScreen';
import RaspberryScreen from '@/pages/RaspberryScreen';
import SettingsScreen from '@/pages/SettingsScreen';
import SubscriptionPage from '@/pages/SubscriptionPage';

// El Guarda de Seguretat
const ProtectedRoute = ({ children }: { children: JSX.Element }) => {
  const token = localStorage.getItem('token');
  if (!token) {
    return <Navigate to="/login" replace />;
  }
  // SI HI HA TOKEN, LI POSEM L'EMBOLCALL DEL MENÚ!
  return <MainLayout>{children}</MainLayout>; 
};

function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/" element={<LandingScreen />} />
        
        <Route path="/login" element={<LoginScreen />} />

        <Route path="/register" element={<RegisterScreen />} />

        <Route path="/forgot-password" element={<ForgotPasswordScreen />} />
        
        <Route path="/pricing" element={<PricingScreen />} />

        <Route path="/classrooms/:id" element={<ClassroomScreen />} />

        <Route path="/student" element={<StudentDashboard />} />

        <Route path="/student/class/:id" element={<StudentClassroomScreen />} />

        <Route path="/student/note/:id" element={<StudentNoteScreen />} />

        <Route path="/raspberry" element={<RaspberryScreen />} />

        <Route path="/settings" element={<SettingsScreen />} />
        
        <Route 
          path="/notes" 
          element={
            <ProtectedRoute>
              <DashboardScreen />
            </ProtectedRoute>
          } 
        />

        <Route 
          path="/notes/:id" 
          element={
            <ProtectedRoute>
              <NoteScreen />
            </ProtectedRoute>
          } 
        />

        <Route 
          path="/subscription" 
          element={
            <ProtectedRoute>
              <SubscriptionPage />
            </ProtectedRoute>
          } 
        />

        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
      <Toaster position="bottom-right" richColors/>
    </BrowserRouter>
  );
}

export default App;