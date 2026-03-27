
import { BrowserRouter, Routes, Route } from 'react-router-dom';
import { Layout } from './components/Layout';
import { ToastProvider } from './components/ToastContext';
import { Home } from './pages/Home';
import { Classrooms } from './pages/Classrooms';
import { ClassroomDetail } from './pages/ClassroomDetail';
import { Teachers } from './pages/Teachers';

export default function App() {
  return (
    <ToastProvider>
      <BrowserRouter>
        <Routes>
          <Route path="/" element={<Layout />}>
            <Route index element={<Home />} />
            <Route path="classrooms" element={<Classrooms />} />
            <Route path="classrooms/:id" element={<ClassroomDetail />} />
            <Route path="teachers" element={<Teachers />} />
          </Route>
        </Routes>
      </BrowserRouter>
    </ToastProvider>
  );
}
