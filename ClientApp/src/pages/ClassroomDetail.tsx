import React, { useEffect, useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { ClassroomService, StudentService } from '../services/api';
import { useToast } from '../components/ToastContext';
import { ArrowLeft, Users, Plus, Edit, Trash2, X, Star } from 'lucide-react';

export const ClassroomDetail = () => {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const toast = useToast();
  const classroomId = Number(id);

  const [classroom, setClassroom] = useState<any>(null);
  const [loading, setLoading] = useState(true);

  // Add Student Modal
  const [showAddModal, setShowAddModal] = useState(false);
  const [addForm, setAddForm] = useState({ userName: '', repositoryUrl: '' });

  // Edit Student Modal
  const [showEditModal, setShowEditModal] = useState(false);
  const [editStudent, setEditStudent] = useState<any>(null);
  const [editForm, setEditForm] = useState({ name: '', gitHubUsername: '', email: '', isLeader: false });

  const [submitting, setSubmitting] = useState(false);
  const [search, setSearch] = useState('');

  const fetchClassroom = async () => {
    setLoading(true);
    try {
      const res = await ClassroomService.getById(classroomId);
      setClassroom(res.data);
    } catch { toast.show('Failed to load classroom', 'error'); }
    finally { setLoading(false); }
  };

  useEffect(() => { fetchClassroom(); }, [classroomId]);

  // Add student via import endpoint
  const handleAddStudent = async (e: React.FormEvent) => {
    e.preventDefault();
    setSubmitting(true);
    try {
      const res = await ClassroomService.importStudents(classroomId, [addForm]);
      toast.show(res.data.message || 'Student added!', 'success');
      setShowAddModal(false);
      setAddForm({ userName: '', repositoryUrl: '' });
      fetchClassroom();
    } catch (err: any) { toast.show(err.response?.data || 'Failed to add student', 'error'); }
    finally { setSubmitting(false); }
  };

  // Edit student
  const openEditModal = (s: any) => {
    setEditStudent(s);
    setEditForm({ name: s.name, gitHubUsername: s.gitHubUsername, email: s.email || '', isLeader: s.isLeader || false });
    setShowEditModal(true);
  };

  const handleEditStudent = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!editStudent) return;
    setSubmitting(true);
    try {
      await StudentService.update(editStudent.id, editForm);
      toast.show('Student updated!', 'success');
      setShowEditModal(false);
      fetchClassroom();
    } catch (err: any) { toast.show(err.response?.data || 'Failed to update', 'error'); }
    finally { setSubmitting(false); }
  };

  // Delete student
  const handleDeleteStudent = async (studentId: number) => {
    if (!confirm('Delete this student?')) return;
    try {
      await ClassroomService.removeStudent(classroomId, studentId);
      toast.show('Student removed', 'success');
      fetchClassroom();
    } catch (err: any) { toast.show('Failed to delete student', 'error'); }
  };

  if (loading) return <div className="card empty-state">Loading classroom details...</div>;
  if (!classroom) return <div className="card empty-state">Classroom not found.</div>;

  return (
    <div>
      <div className="page-header">
        <div style={{ display: 'flex', alignItems: 'center', gap: '1rem' }}>
          <button className="btn btn-outline" onClick={() => navigate('/classrooms')}><ArrowLeft size={18} /></button>
          <div>
            <h1 className="page-title">{classroom.name}</h1>
            <span style={{ color: 'var(--text-muted)', fontSize: '0.9rem' }}>Classroom ID: {classroom.id}</span>
          </div>
        </div>
        <button className="btn btn-primary" onClick={() => setShowAddModal(true)}>
          <Plus size={18} /> Add Student
        </button>
      </div>

      {/* Search */}
      <div style={{ marginBottom: '1.5rem' }}>
        <input className="form-control" style={{ maxWidth: '400px' }} placeholder="Search students by name, code, username..." value={search} onChange={e => setSearch(e.target.value)} />
      </div>

      {/* Groups */}
      {classroom.groups && classroom.groups.length > 0 ? (
        classroom.groups.map((group: any) => {
          const filteredStudents = group.students?.filter((s: any) =>
            !search || s.name.toLowerCase().includes(search.toLowerCase()) ||
            s.studentCode.toLowerCase().includes(search.toLowerCase()) ||
            s.gitHubUsername.toLowerCase().includes(search.toLowerCase())
          ) || [];

          return (
            <div className="card" key={group.id} style={{ marginBottom: '1.5rem' }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '1rem' }}>
                <div>
                  <h3 style={{ margin: 0 }}>{group.groupName}</h3>
                  <a href={group.repositoryUrl} target="_blank" rel="noreferrer" style={{ fontSize: '0.8rem', color: 'var(--primary)' }}>{group.repositoryUrl}</a>
                </div>
                <span style={{
                  padding: '0.25rem 0.5rem', borderRadius: '1rem', fontSize: '0.75rem', fontWeight: 600,
                  backgroundColor: group.status === 'Active' ? 'rgba(16, 185, 129, 0.1)' : 'rgba(239, 68, 68, 0.1)',
                  color: group.status === 'Active' ? 'var(--secondary-hover)' : 'var(--danger-hover)'
                }}>{group.status}</span>
              </div>
              {filteredStudents.length > 0 ? (
                <div className="table-container">
                  <table>
                    <thead>
                      <tr>
                        <th>Code</th>
                        <th>Student</th>
                        <th>GitHub</th>
                        <th>Role</th>
                        <th>Actions</th>
                      </tr>
                    </thead>
                    <tbody>
                      {filteredStudents.map((s: any) => (
                        <tr key={s.id}>
                          <td><span style={{ fontFamily: 'monospace', fontWeight: 600 }}>{s.studentCode}</span></td>
                          <td>
                            <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
                              {s.avatarUrl && <img src={s.avatarUrl} alt="" className="avatar-sm" />}
                              <b>{s.name}</b>
                            </div>
                          </td>
                          <td><a href={`https://github.com/${s.gitHubUsername}`} target="_blank" rel="noreferrer">@{s.gitHubUsername}</a></td>
                          <td>
                            {s.isLeader && <span style={{
                              display: 'inline-flex', alignItems: 'center', gap: '0.25rem',
                              padding: '0.2rem 0.5rem', borderRadius: '1rem', fontSize: '0.7rem', fontWeight: 700,
                              backgroundColor: 'rgba(245, 158, 11, 0.15)', color: '#d97706'
                            }}><Star size={12} /> Leader</span>}
                          </td>
                          <td style={{ display: 'flex', gap: '0.5rem' }}>
                            <button className="btn btn-outline" style={{ padding: '0.25rem 0.5rem' }} onClick={() => openEditModal(s)}><Edit size={16} /></button>
                            <button className="btn btn-danger" style={{ padding: '0.25rem 0.5rem' }} onClick={() => handleDeleteStudent(s.id)}><Trash2 size={16} /></button>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              ) : <div className="empty-state">No students in this group{search ? ' matching your search' : ''}.</div>}
            </div>
          );
        })
      ) : (
        <div className="card empty-state">
          <Users size={48} style={{ opacity: 0.2, marginBottom: '1rem' }} />
          <h3>No Groups Yet</h3>
          <p>Import students or add a student to create groups automatically.</p>
        </div>
      )}

      {/* Add Student Modal */}
      {showAddModal && (
        <div className="modal-overlay">
          <div className="modal-content">
            <div className="modal-header">
              <h3>Add Student to Classroom</h3>
              <button className="btn btn-outline" style={{ border: 'none', padding: '0.25rem' }} onClick={() => setShowAddModal(false)}><X size={20} /></button>
            </div>
            <form onSubmit={handleAddStudent}>
              <div className="modal-body">
                <p style={{ marginBottom: '1rem', color: 'var(--text-muted)', fontSize: '0.9rem' }}>
                  A new group will be created automatically based on the repository URL if one doesn't exist.
                </p>
                <div className="form-group">
                  <label className="form-label">GitHub Username</label>
                  <input required className="form-control" value={addForm.userName} onChange={e => setAddForm({ ...addForm, userName: e.target.value })} placeholder="e.g. johndoe" />
                </div>
                <div className="form-group">
                  <label className="form-label">Repository URL</label>
                  <input required type="url" className="form-control" value={addForm.repositoryUrl} onChange={e => setAddForm({ ...addForm, repositoryUrl: e.target.value })} placeholder="https://github.com/owner/repo" />
                </div>
              </div>
              <div className="modal-footer">
                <button type="button" className="btn btn-outline" onClick={() => setShowAddModal(false)}>Cancel</button>
                <button type="submit" className="btn btn-primary" disabled={submitting}>{submitting ? 'Adding...' : 'Add Student'}</button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Edit Student Modal */}
      {showEditModal && editStudent && (
        <div className="modal-overlay">
          <div className="modal-content">
            <div className="modal-header">
              <h3>Edit Student: {editStudent.studentCode}</h3>
              <button className="btn btn-outline" style={{ border: 'none', padding: '0.25rem' }} onClick={() => setShowEditModal(false)}><X size={20} /></button>
            </div>
            <form onSubmit={handleEditStudent}>
              <div className="modal-body">
                <div className="form-group">
                  <label className="form-label">Name</label>
                  <input required className="form-control" value={editForm.name} onChange={e => setEditForm({ ...editForm, name: e.target.value })} />
                </div>
                <div className="form-group">
                  <label className="form-label">GitHub Username</label>
                  <input required className="form-control" value={editForm.gitHubUsername} onChange={e => setEditForm({ ...editForm, gitHubUsername: e.target.value })} />
                </div>
                <div className="form-group">
                  <label className="form-label">Email</label>
                  <input type="email" className="form-control" value={editForm.email} onChange={e => setEditForm({ ...editForm, email: e.target.value })} placeholder="optional" />
                </div>
                <div className="form-group" style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', marginTop: '1rem' }}>
                  <input type="checkbox" id="isLeader" checked={editForm.isLeader} onChange={e => setEditForm({ ...editForm, isLeader: e.target.checked })} style={{ width: '18px', height: '18px' }} />
                  <label htmlFor="isLeader" style={{ fontWeight: 500 }}>Group Leader</label>
                </div>
              </div>
              <div className="modal-footer">
                <button type="button" className="btn btn-outline" onClick={() => setShowEditModal(false)}>Cancel</button>
                <button type="submit" className="btn btn-primary" disabled={submitting}>{submitting ? 'Saving...' : 'Save Changes'}</button>
              </div>
            </form>
          </div>
        </div>
      )}

      <style>{`
        .avatar-sm { width: 32px; height: 32px; border-radius: 50%; object-fit: cover; }
      `}</style>
    </div>
  );
};
