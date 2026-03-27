import React, { useEffect, useState } from 'react';
import { TeacherService } from '../services/api';
import { useToast } from '../components/ToastContext';
import { Users, Plus, Trash2, Edit, X, Search } from 'lucide-react';

export const Teachers = () => {
  const toast = useToast();
  const [teachers, setTeachers] = useState<any[]>([]);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState('');

  const [showModal, setShowModal] = useState(false);
  const [editingTeacher, setEditingTeacher] = useState<any>(null);
  const [formData, setFormData] = useState({ username: '', name: '', email: '' });
  const [submitting, setSubmitting] = useState(false);

  const fetchTeachers = async () => {
    setLoading(true);
    try { const res = await TeacherService.getAll(); setTeachers(res.data); }
    catch { toast.show('Failed to load teachers', 'error'); }
    finally { setLoading(false); }
  };

  useEffect(() => { fetchTeachers(); }, []);

  const filtered = teachers.filter(t => {
    if (!search) return true;
    const s = search.toLowerCase();
    return t.name.toLowerCase().includes(s) || t.username.toLowerCase().includes(s) || t.email?.toLowerCase().includes(s);
  });

  const openCreate = () => { setEditingTeacher(null); setFormData({ username: '', name: '', email: '' }); setShowModal(true); };
  const openEdit = (t: any) => { setEditingTeacher(t); setFormData({ username: t.username, name: t.name, email: t.email }); setShowModal(true); };

  const handleDelete = async (id: number) => {
    if (!confirm('Delete this teacher?')) return;
    try { await TeacherService.delete(id); toast.show('Teacher deleted', 'success'); fetchTeachers(); }
    catch { toast.show('Failed to delete', 'error'); }
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault(); setSubmitting(true);
    try {
      if (editingTeacher) { await TeacherService.update(editingTeacher.id, { name: formData.name, email: formData.email }); toast.show('Teacher updated!', 'success'); }
      else { await TeacherService.create(formData); toast.show('Teacher created!', 'success'); }
      setShowModal(false); fetchTeachers();
    } catch (err: any) { toast.show(err.response?.data || 'Error saving teacher', 'error'); }
    finally { setSubmitting(false); }
  };

  return (
    <div>
      <div className="page-header">
        <h1 className="page-title"><Users size={28} style={{ display: 'inline-block', verticalAlign: 'middle', marginRight: '0.5rem' }} /> Teachers</h1>
        <button className="btn btn-primary" onClick={openCreate}><Plus size={18} /> Add Teacher</button>
      </div>

      <div style={{ marginBottom: '1.5rem', display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
        <Search size={18} style={{ color: 'var(--text-muted)' }} />
        <input className="form-control" style={{ maxWidth: '350px' }} placeholder="Search by name, username, email..." value={search} onChange={e => setSearch(e.target.value)} />
      </div>

      <div className="card">
        {loading ? <div className="empty-state">Loading teachers...</div> : filtered.length === 0 ? <div className="empty-state">No teachers found.</div> : (
          <div className="table-container">
            <table>
              <thead><tr><th>ID</th><th>Username</th><th>Name</th><th>Email</th><th>Actions</th></tr></thead>
              <tbody>
                {filtered.map(t => (
                  <tr key={t.id}>
                    <td>{t.id}</td><td>{t.username}</td><td><b>{t.name}</b></td><td>{t.email}</td>
                    <td style={{ display: 'flex', gap: '0.5rem' }}>
                      <button className="btn btn-outline" style={{ padding: '0.25rem 0.5rem' }} onClick={() => openEdit(t)}><Edit size={16} /></button>
                      <button className="btn btn-danger" style={{ padding: '0.25rem 0.5rem' }} onClick={() => handleDelete(t.id)}><Trash2 size={16} /></button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>

      {showModal && (
        <div className="modal-overlay"><div className="modal-content">
          <div className="modal-header"><h3>{editingTeacher ? 'Edit Teacher' : 'Add New Teacher'}</h3><button className="btn btn-outline" style={{ border: 'none', padding: '0.25rem' }} onClick={() => setShowModal(false)}><X size={20} /></button></div>
          <form onSubmit={handleSubmit}><div className="modal-body">
            <div className="form-group"><label className="form-label">Username</label><input required className="form-control" disabled={!!editingTeacher} value={formData.username} onChange={e => setFormData({ ...formData, username: e.target.value })} placeholder="e.g. teacher01" /></div>
            <div className="form-group"><label className="form-label">Full Name</label><input required className="form-control" value={formData.name} onChange={e => setFormData({ ...formData, name: e.target.value })} placeholder="e.g. Nguyen Van A" /></div>
            <div className="form-group"><label className="form-label">Email</label><input required type="email" className="form-control" value={formData.email} onChange={e => setFormData({ ...formData, email: e.target.value })} placeholder="teacher@example.com" /></div>
          </div><div className="modal-footer"><button type="button" className="btn btn-outline" onClick={() => setShowModal(false)}>Cancel</button><button type="submit" className="btn btn-primary" disabled={submitting}>{submitting ? 'Saving...' : 'Save'}</button></div></form>
        </div></div>
      )}
    </div>
  );
};
