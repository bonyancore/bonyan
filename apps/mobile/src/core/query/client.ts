import { QueryClient } from '@tanstack/react-query';

const minute = 60 * 1000;

export const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      networkMode: 'offlineFirst',
      staleTime: minute,
      gcTime: 24 * 60 * minute,
      retry: 2,
    },
    mutations: {
      networkMode: 'offlineFirst',
    },
  },
});
