export interface Note {
  id: number;
  title: string;
  content: string;
  createdAt: string;
  classroomId: number | null;
}

export interface Classroom {
  id: number;
  name: string;
  color: string;
  code: string;
}

export interface UserProfile {
  username: string;
  email: string;
  role: 'professor' | 'alumne';
  preferredLanguage: string;
  secondsAvailable: number;
  maxSeconds: number;
  planName?: string;
}

export interface Student {
  id: number;
  username: string;
  email: string;
}

export interface SubscriptionPlan {
  id: number;
  name: string;
  description: string;
  priceMonthly: number;
  minutesPerMonth: number;
  secondsPerMonth: number;
}

export interface MySubscription {
  hasSubscription: boolean;
  planName?: string;
  startDate?: string;
  nextBillingDate?: string;
  secondsPerMonth?: number;
}

export interface PaginatedResponse<T> {
  totalItems: number;
  page: number;
  pageSize: number;
  totalPages: number;
  items: T[];
}
