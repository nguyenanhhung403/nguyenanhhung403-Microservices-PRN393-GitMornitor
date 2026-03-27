import axios from 'axios';

const apiBase = '/api';

const apiClient = axios.create({
  baseURL: apiBase,
  headers: {
    'Content-Type': 'application/json',
  },
});

export const TeacherService = {
  getAll: () => apiClient.get('/teachers'),
  getById: (id: number) => apiClient.get(`/teachers/${id}`),
  create: (data: any) => apiClient.post('/teachers', data),
  update: (id: number, data: any) => apiClient.put(`/teachers/${id}`, data),
  delete: (id: number) => apiClient.delete(`/teachers/${id}`),
};

export const ClassroomService = {
  getAll: () => apiClient.get('/classrooms'),
  getById: (id: number) => apiClient.get(`/classrooms/${id}`),
  create: (data: any) => apiClient.post('/classrooms', data),
  update: (id: number, data: any) => apiClient.put(`/classrooms/${id}`, data),
  delete: (id: number) => apiClient.delete(`/classrooms/${id}`),
  configureToken: (id: number, data: any) => apiClient.put(`/classrooms/${id}/token`, data),
  importStudents: (id: number, data: any) => apiClient.post(`/classrooms/${id}/import`, data),
  removeStudent: (id: number, studentId: number) => apiClient.delete(`/classrooms/${id}/students/${studentId}`),
};

export const StudentService = {
  getAll: (classRoomId?: number) => apiClient.get('/students', { params: classRoomId ? { classRoomId } : {} }),
  getById: (id: number) => apiClient.get(`/students/${id}`),
  update: (id: number, data: any) => apiClient.put(`/students/${id}`, data),
  delete: (id: number) => apiClient.delete(`/students/${id}`),
};

export const MonitoringService = {
  getDashboard: (classroomId: number) => apiClient.get(`/dashboard/${classroomId}`),
  syncData: (classroomId: number) => apiClient.post(`/sync/${classroomId}`),
  getSyncHistory: (classroomId: number) => apiClient.get(`/sync-history/${classroomId}`),
};
