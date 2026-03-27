import React, { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { ClassroomService, TeacherService } from '../services/api';
import { useToast } from '../components/ToastContext';
import { BookOpen, Plus, Trash2, Edit, Key, Download, X, Search } from 'lucide-react';

export const Classrooms = () => {
  const toast = useToast();
  const navigate = useNavigate();
  const [classrooms, setClassrooms] = useState<any[]>([]);
  const [teachers, setTeachers] = useState<any[]>([]);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState('');

  const [showClassModal, setShowClassModal] = useState(false);
  const [editingClass, setEditingClass] = useState<any>(null);
  const [classForm, setClassForm] = useState({ name: '', teacherId: '', isActive: true });

  const [showTokenModal, setShowTokenModal] = useState(false);
  const [tokenId, setTokenId] = useState<number | null>(null);
  const [tokenString, setTokenString] = useState('');

  const [showImportModal, setShowImportModal] = useState(false);
  const [importId, setImportId] = useState<number | null>(null);
  const [importJson, setImportJson] = useState('');

  const [submitting, setSubmitting] = useState(false);

  const fetchData = async () => {
    try {
      setLoading(true);
      const [crRes, tRes] = await Promise.all([ClassroomService.getAll(), TeacherService.getAll()]);
      setClassrooms(crRes.data);
      setTeachers(tRes.data);
    } catch { toast.show('Failed to load data', 'error'); }
    finally { setLoading(false); }
  };

  useEffect(() => { fetchData(); }, []);

  const filtered = classrooms.filter(c => {
    if (!search) return true;
    const s = search.toLowerCase();
    const teacher = teachers.find(t => t.id === c.teacherId);
    return c.name.toLowerCase().includes(s) || (teacher?.name?.toLowerCase().includes(s));
  });

  const openCreateClass = () => { setEditingClass(null); setClassForm({ name: '', teacherId: '', isActive: true }); setShowClassModal(true); };
  const openEditClass = (c: any) => { setEditingClass(c); setClassForm({ name: c.name, teacherId: c.teacherId, isActive: c.isActive }); setShowClassModal(true); };

  const handleSaveClass = async (e: React.FormEvent) => {
    e.preventDefault(); setSubmitting(true);
    try {
      if (editingClass) { await ClassroomService.update(editingClass.id, { name: classForm.name, isActive: classForm.isActive }); toast.show('Classroom updated!', 'success'); }
      else { await ClassroomService.create({ name: classForm.name, teacherId: Number(classForm.teacherId) }); toast.show('Classroom created!', 'success'); }
      setShowClassModal(false); fetchData();
    } catch (err: any) { toast.show(err.response?.data?.Message || err.message, 'error'); }
    finally { setSubmitting(false); }
  };

  const handleDeleteClass = async (id: number) => {
    if (!confirm('Delete this classroom and all its data?')) return;
    try { await ClassroomService.delete(id); toast.show('Classroom deleted', 'success'); fetchData(); }
    catch { toast.show('Failed to delete', 'error'); }
  };

  const openTokenModal = (c: any) => { setTokenId(c.id); setTokenString(''); setShowTokenModal(true); };
  const handleSaveToken = async (e: React.FormEvent) => {
    e.preventDefault(); setSubmitting(true);
    try { if (tokenId) { const res = await ClassroomService.configureToken(tokenId, { token: tokenString }); toast.show(res.data.message || 'Token applied!', 'success'); } setShowTokenModal(false); }
    catch (err: any) { toast.show(err.response?.data?.Message || err.message, 'error'); }
    finally { setSubmitting(false); }
  };

  const openImportModal = (c: any) => { setImportId(c.id); setImportJson('[\n  {\n    "UserName": "github_username",\n    "RepositoryUrl": "https://github.com/owner/repo"\n  }\n]'); setShowImportModal(true); };
  const handleImport = async (e: React.FormEvent) => {
    e.preventDefault(); setSubmitting(true);
    try {
      const data = JSON.parse(importJson);
      if (importId) { const res = await ClassroomService.importStudents(importId, data); toast.show(res.data.message || 'Students imported!', 'success'); fetchData(); }
      setShowImportModal(false);
    } catch (err: any) {
      if (err instanceof SyntaxError) toast.show('Invalid JSON format', 'error');
      else toast.show(err.response?.data?.Message || err.message, 'error');
    } finally { setSubmitting(false); }
  };

  return (
    <div>
      <div className="page-header">
        <h1 className="page-title"><BookOpen size={28} style={{ display: 'inline-block', verticalAlign: 'middle', marginRight: '0.5rem' }} /> Classrooms</h1>
        <button className="btn btn-primary" onClick={openCreateClass}><Plus size={18} /> Add Classroom</button>
      </div>

      <div style={{ marginBottom: '1.5rem', display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
        <Search size={18} style={{ color: 'var(--text-muted)' }} />
        <input className="form-control" style={{ maxWidth: '350px' }} placeholder="Search by name or teacher..." value={search} onChange={e => setSearch(e.target.value)} />
      </div>

      <div className="card">
        {loading ? <div className="empty-state">Loading classrooms...</div> : filtered.length === 0 ? <div className="empty-state">No classrooms found.</div> : (
          <div className="table-container">
            <table>
              <thead><tr><th>ID</th><th>Name</th><th>Teacher</th><th>Status</th><th>Groups</th><th>Students</th><th>Actions</th></tr></thead>
              <tbody>
                {filtered.map(c => {
                  const teacher = teachers.find(t => t.id === c.teacherId);
                  return (
                    <tr key={c.id}>
                      <td>{c.id}</td>
                      <td><b className="clickable-name" onClick={() => navigate(`/classrooms/${c.id}`)}>{c.name}</b></td>
                      <td>{teacher?.name || 'Unknown'}</td>
                      <td><span className={`status-badge ${c.isActive ? 'status-active' : 'status-archived'}`}>{c.isActive ? 'Active' : 'Archived'}</span></td>
                      <td>{c.studentGroupsCount}</td>
                      <td>{c.studentsCount}</td>
                      <td style={{ display: 'flex', gap: '0.5rem' }}>
                        <button className="btn btn-outline" style={{ padding: '0.25rem 0.5rem' }} title="Configure Token" onClick={() => openTokenModal(c)}><Key size={16} /></button>
                        <button className="btn btn-outline" style={{ padding: '0.25rem 0.5rem' }} title="Import Students" onClick={() => openImportModal(c)}><Download size={16} /></button>
                        <button className="btn btn-outline" style={{ padding: '0.25rem 0.5rem' }} title="Edit" onClick={() => openEditClass(c)}><Edit size={16} /></button>
                        <button className="btn btn-danger" style={{ padding: '0.25rem 0.5rem' }} title="Delete" onClick={() => handleDeleteClass(c.id)}><Trash2 size={16} /></button>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}
      </div>

      {showClassModal && (
        <div className="modal-overlay"><div className="modal-content">
          <div className="modal-header"><h3>{editingClass ? 'Edit Classroom' : 'Add New Classroom'}</h3><button className="btn btn-outline" style={{ border: 'none', padding: '0.25rem' }} onClick={() => setShowClassModal(false)}><X size={20} /></button></div>
          <form onSubmit={handleSaveClass}><div className="modal-body">
            <div className="form-group"><label className="form-label">Classroom Name</label><input required className="form-control" value={classForm.name} onChange={e => setClassForm({ ...classForm, name: e.target.value })} placeholder="e.g. PRN393" /></div>
            {!editingClass && <div className="form-group"><label className="form-label">Teacher</label><select required className="form-control" value={classForm.teacherId} onChange={e => setClassForm({ ...classForm, teacherId: e.target.value })}><option value="">-- Assign a Teacher --</option>{teachers.map(t => <option key={t.id} value={t.id}>{t.name}</option>)}</select></div>}
            {editingClass && <div className="form-group" style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', marginTop: '1rem' }}><input type="checkbox" id="isActive" checked={classForm.isActive} onChange={e => setClassForm({ ...classForm, isActive: e.target.checked })} style={{ width: '18px', height: '18px' }} /><label htmlFor="isActive" style={{ fontWeight: 500 }}>Active</label></div>}
          </div><div className="modal-footer"><button type="button" className="btn btn-outline" onClick={() => setShowClassModal(false)}>Cancel</button><button type="submit" className="btn btn-primary" disabled={submitting}>{submitting ? 'Saving...' : 'Save'}</button></div></form>
        </div></div>
      )}

      {showTokenModal && (
        <div className="modal-overlay"><div className="modal-content">
          <div className="modal-header"><h3>Configure GitHub Token</h3><button className="btn btn-outline" style={{ border: 'none', padding: '0.25rem' }} onClick={() => setShowTokenModal(false)}><X size={20} /></button></div>
          <form onSubmit={handleSaveToken}><div className="modal-body">
            <p style={{ marginBottom: '1rem', color: 'var(--text-muted)' }}>Apply a GitHub PAT to all groups in this classroom.</p>
            <div className="form-group"><label className="form-label">GitHub Token</label><input required type="password" className="form-control" value={tokenString} onChange={e => setTokenString(e.target.value)} placeholder="github_pat_..." /></div>
          </div><div className="modal-footer"><button type="button" className="btn btn-outline" onClick={() => setShowTokenModal(false)}>Cancel</button><button type="submit" className="btn btn-primary" disabled={submitting}>{submitting ? 'Applying...' : 'Apply Token'}</button></div></form>
        </div></div>
      )}

      {showImportModal && (
        <div className="modal-overlay"><div className="modal-content">
          <div className="modal-header"><h3>Import Students JSON</h3><button className="btn btn-outline" style={{ border: 'none', padding: '0.25rem' }} onClick={() => setShowImportModal(false)}><X size={20} /></button></div>
          <form onSubmit={handleImport}><div className="modal-body">
            <p style={{ marginBottom: '0.5rem', color: 'var(--text-muted)' }}>Paste JSON array of students.</p>
            <div className="form-group"><textarea required className="form-control" rows={10} style={{ fontFamily: 'monospace', fontSize: '0.85rem' }} value={importJson} onChange={e => setImportJson(e.target.value)} /></div>
          </div><div className="modal-footer"><button type="button" className="btn btn-outline" onClick={() => setShowImportModal(false)}>Cancel</button><button type="submit" className="btn btn-primary" disabled={submitting}>{submitting ? 'Importing...' : 'Import'}</button></div></form>
        </div></div>
      )}
    </div>
  );
};
