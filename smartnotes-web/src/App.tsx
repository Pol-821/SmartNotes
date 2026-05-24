import React, { useEffect } from 'react';
import { BrowserRouter, Routes, Route, Navigate, useNavigate } from 'react-router-dom';
import { ThemeProvider } from "next-themes";
import { Toaster } from "@/components/ui/sonner";
import ErrorBoundary from '@/components/ErrorBoundary';
import { UserProvider } from '@/contexts/UserContext';
import { setNavigateFn } from '@/services/api';
import { getToken, isTokenExpired, clearAuth } from '@/lib/auth';
import { FullPageLoader } from '@/components/ui/spinner';

const LandingScreen = React.lazy(() => import('./pages/LandingScreen'));
const LoginScreen = React.lazy(() => import('./pages/LoginScreen'));
const MainLayout = React.lazy(() => import('./components/layout/MainLayout'));
const DashboardScreen = React.lazy(() => import('./pages/DashboardScreen'));
const RegisterScreen = React.lazy(() => import('./pages/RegisterScreen'));
const ForgotPasswordScreen = React.lazy(() => import('./pages/ForgotPasswordScreen'));
const PricingScreen = React.lazy(() => import('./pages/PricingScreen'));
const NoteScreen = React.lazy(() => import('./pages/NoteScreen'));
const ClassroomScreen = React.lazy(() => import('./pages/ClassroomScreen'));
const StudentDashboard = React.lazy(() => import('./pages/StudentDashboard'));
const StudentClassroomScreen = React.lazy(() => import('./pages/StudentClassroomScreen'));
const StudentNoteScreen = React.lazy(() => import('./pages/StudentNoteScreen'));
const RaspberryScreen = React.lazy(() => import('./pages/RaspberryScreen'));
const SettingsScreen = React.lazy(() => import('./pages/SettingsScreen'));
const SubscriptionPage = React.lazy(() => import('./pages/SubscriptionPage'));

const ProtectedRoute = ({ children }: { children: React.ReactNode }) => {
  const token = getToken();
  if (!token || isTokenExpired(token)) {
    if (token) { clearAuth(); }
    return <Navigate to="/login" replace />;
  }
  return <MainLayout>{children}</MainLayout>;
};

const StudentLayout = ({ children }: { children: React.ReactNode }) => {
  const token = getToken();
  if (!token || isTokenExpired(token)) {
    if (token) { clearAuth(); }
    return <Navigate to="/login" replace />;
  }
  return <>{children}</>;
};

function AppContent() {
  const navigate = useNavigate();
  useEffect(() => { setNavigateFn(navigate); }, [navigate]);
  return (
    <ThemeProvider attribute="class" defaultTheme="system" enableSystem>
    <ErrorBoundary>
    <React.Suspense fallback={<FullPageLoader text="Carregant..." />}>
    <UserProvider>
    <Routes>
      <Route path="/" element={<LandingScreen />} />
      <Route path="/login" element={<LoginScreen />} />
      <Route path="/register" element={<RegisterScreen />} />
      <Route path="/forgot-password" element={<ForgotPasswordScreen />} />
      <Route path="/pricing" element={<PricingScreen />} />
      <Route path="/classrooms/:id" element={<ProtectedRoute><ClassroomScreen /></ProtectedRoute>} />
      <Route path="/student" element={<StudentLayout><StudentDashboard /></StudentLayout>} />
      <Route path="/student/class/:id" element={<StudentLayout><StudentClassroomScreen /></StudentLayout>} />
      <Route path="/student/note/:id" element={<StudentLayout><StudentNoteScreen /></StudentLayout>} />
      <Route path="/raspberry" element={<ProtectedRoute><RaspberryScreen /></ProtectedRoute>} />
      <Route path="/settings" element={<ProtectedRoute><SettingsScreen /></ProtectedRoute>} />
      <Route path="/notes" element={<ProtectedRoute><DashboardScreen /></ProtectedRoute>} />
      <Route path="/notes/:id" element={<ProtectedRoute><NoteScreen /></ProtectedRoute>} />
      <Route path="/subscription" element={<ProtectedRoute><SubscriptionPage /></ProtectedRoute>} />
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
    </UserProvider>
    </React.Suspense>
    <Toaster position="bottom-right" richColors/>
    </ErrorBoundary>
    </ThemeProvider>
  );
}

function App() {
  return (
    <BrowserRouter>
      <AppContent />
    </BrowserRouter>
  );
}

export default App;
