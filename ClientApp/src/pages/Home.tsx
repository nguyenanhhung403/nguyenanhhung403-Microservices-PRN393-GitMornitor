import React, { useEffect, useState } from 'react';
import { ClassroomService, MonitoringService } from '../services/api';
import { useToast } from '../components/ToastContext';
import { LayoutDashboard, RefreshCw, BarChart as BarChartIcon, Trophy, GitCommit, Code, CheckCircle, AlertCircle, ChevronDown, ChevronRight, Clock, User, PieChart as PieChartIcon } from 'lucide-react';
import { BarChart, Bar, XAxis, YAxis, Tooltip, ResponsiveContainer, CartesianGrid, Legend, PieChart, Pie, Cell, AreaChart, Area, Line } from 'recharts';

export const Home = () => {
  const toast = useToast();
  const [classrooms, setClassrooms] = useState<any[]>([]);
  const [selectedClassId, setSelectedClassId] = useState<number | ''>(() => {
    const saved = localStorage.getItem('selectedClassId');
    return saved ? Number(saved) : '';
  });
  const [dashboardData, setDashboardData] = useState<any>(null);
  const [syncHistory, setSyncHistory] = useState<any[]>([]);
  const [loading, setLoading] = useState(false);
  const [syncing, setSyncing] = useState(false);
  const [expandedRepos, setExpandedRepos] = useState<Record<number, boolean>>({});
  const [showHistory, setShowHistory] = useState(false);
  const [activeTab, setActiveTab] = useState<'leaderboard' | 'students'>('leaderboard');

  useEffect(() => {
    ClassroomService.getAll().then(res => {
      setClassrooms(res.data);
      // Auto-load dashboard if we had a saved selection
      if (selectedClassId) loadDashboard(selectedClassId as number);
    }).catch(() => toast.show('Failed to load classrooms', 'error'));
  }, []);

  const loadDashboard = async (id: number) => {
    setLoading(true);
    try {
      const [dashRes, histRes] = await Promise.all([
        MonitoringService.getDashboard(id),
        MonitoringService.getSyncHistory(id)
      ]);
      setDashboardData(dashRes.data);
      setSyncHistory(histRes.data?.batches || []);
    } catch { setDashboardData(null); setSyncHistory([]); }
    finally { setLoading(false); }
  };

  const handleSelectChange = (e: React.ChangeEvent<HTMLSelectElement>) => {
    const val = e.target.value;
    const numVal = val ? Number(val) : '';
    setSelectedClassId(numVal);
    if (numVal) { localStorage.setItem('selectedClassId', String(numVal)); loadDashboard(numVal); }
    else { localStorage.removeItem('selectedClassId'); setDashboardData(null); setSyncHistory([]); }
  };

  const handleSync = async () => {
    if (!selectedClassId) return;
    setSyncing(true);
    try {
      const res = await MonitoringService.syncData(selectedClassId as number);
      toast.show(res.data.message || 'Sync completed!', 'success');
      await loadDashboard(selectedClassId as number);
    } catch { toast.show('Sync failed. Check GitHub token.', 'error'); }
    finally { setSyncing(false); }
  };

  const toggleRepo = (idx: number) => setExpandedRepos(prev => ({ ...prev, [idx]: !prev[idx] }));

  const lastSyncTime = dashboardData?.leaderboard?.[0]?.lastSync;
  const totalAdded = dashboardData?.leaderboard?.reduce((s: number, i: any) => s + i.linesAdded, 0) || 0;
  const totalDeleted = dashboardData?.leaderboard?.reduce((s: number, i: any) => s + i.linesDeleted, 0) || 0;

  // Per-student breakdown: group by student across repos
  const studentInsights = React.useMemo(() => {
    if (!dashboardData) return [];
    const map = new Map<string, any>();
    dashboardData.repositories?.forEach((repo: any) => {
      repo.contributors?.forEach((c: any) => {
        if (c.isExternal) return;
        const key = c.username;
        if (!map.has(key)) map.set(key, { username: key, avatar: c.avatar, repos: [], totalCommits: 0, totalAdded: 0, totalDeleted: 0 });
        const entry = map.get(key)!;
        entry.repos.push({ groupName: repo.groupName, commits: c.commits, linesAdded: c.linesAdded, linesDeleted: c.linesDeleted });
        entry.totalCommits += c.commits;
        entry.totalAdded += c.linesAdded;
        entry.totalDeleted += c.linesDeleted;
      });
    });
    return Array.from(map.values()).sort((a, b) => b.totalCommits - a.totalCommits);
  }, [dashboardData]);

  const repoDistribution = React.useMemo(() => {
    if (!dashboardData?.repositories) return [];
    return dashboardData.repositories.map((repo: any) => ({
      name: repo.groupName,
      value: repo.contributors?.reduce((s: number, c: any) => s + c.commits, 0) || 0
    })).filter((r: any) => r.value > 0);
  }, [dashboardData]);

  const COLORS = ['#6366f1', '#10b981', '#f59e0b', '#ef4444', '#8b5cf6', '#ec4899'];

  return (
    <div>
      <div className="page-header" style={{ alignItems: 'flex-start', flexDirection: 'column', gap: '1rem' }}>
        <h1 className="page-title"><LayoutDashboard size={28} style={{ display: 'inline-block', verticalAlign: 'middle', marginRight: '0.5rem' }} /> GitMonitor Dashboard</h1>
        <div style={{ display: 'flex', gap: '1rem', width: '100%', maxWidth: '500px', alignItems: 'center' }}>
          <select className="form-control" value={selectedClassId} onChange={handleSelectChange}>
            <option value="">-- Select a Classroom --</option>
            {classrooms.map(c => <option key={c.id} value={c.id}>{c.name}</option>)}
          </select>
          <button className="btn btn-primary" onClick={handleSync} disabled={!selectedClassId || syncing}>
            <RefreshCw size={18} className={syncing ? 'spinner' : ''} /> {syncing ? 'Syncing...' : 'Sync Git'}
          </button>
        </div>
        {lastSyncTime && <span style={{ fontSize: '0.8rem', color: 'var(--text-muted)' }}>Last synced: {new Date(lastSyncTime).toLocaleString()}</span>}
      </div>

      {!selectedClassId ? (
        <div className="card empty-state">
          <BarChartIcon size={64} style={{ opacity: 0.2, margin: '0 auto 1rem' }} />
          <h3>No Classroom Selected</h3>
          <p>Select a classroom above to view GitHub activity.</p>
        </div>
      ) : loading ? (
        <div className="card empty-state"><div className="skeleton-loader"></div>Loading dashboard...</div>
      ) : dashboardData ? (
        <div className="fade-in">
          {/* Stat Cards */}
          <div className="stats-grid">
            <div className="stat-card glass-card hover-lift">
              <div className="stat-icon" style={{ background: 'var(--grad-primary)', color: 'white' }}><Trophy size={22} /></div>
              <div className="stat-info"><span className="stat-label">Top Contributor</span><span className="stat-value gradient-text">{dashboardData.leaderboard?.[0]?.name || 'N/A'}</span></div>
            </div>
            <div className="stat-card glass-card hover-lift">
              <div className="stat-icon" style={{ background: 'var(--grad-secondary)', color: 'white' }}><GitCommit size={22} /></div>
              <div className="stat-info"><span className="stat-label">Total Commits</span><span className="stat-value">{dashboardData.leaderboard?.reduce((s: number, i: any) => s + i.commitCount, 0)}</span></div>
            </div>
            <div className="stat-card glass-card hover-lift">
              <div className="stat-icon" style={{ background: 'var(--grad-secondary)', opacity: 0.8, color: 'white' }}><Code size={22} /></div>
              <div className="stat-info"><span className="stat-label">Lines Added</span><span className="stat-value" style={{ color: 'var(--secondary)' }}>+{totalAdded}</span></div>
            </div>
            <div className="stat-card glass-card hover-lift">
              <div className="stat-icon" style={{ background: 'var(--grad-danger)', color: 'white' }}><Code size={22} /></div>
              <div className="stat-info"><span className="stat-label">Lines Deleted</span><span className="stat-value" style={{ color: 'var(--danger)' }}>-{totalDeleted}</span></div>
            </div>
          </div>

          {/* Tabs: Leaderboard + Student Insights */}
          <div className="tab-bar">
            <button className={`tab-btn ${activeTab === 'leaderboard' ? 'tab-active' : ''}`} onClick={() => setActiveTab('leaderboard')}><Trophy size={16} /> Leaderboard</button>
            <button className={`tab-btn ${activeTab === 'students' ? 'tab-active' : ''}`} onClick={() => setActiveTab('students')}><User size={16} /> Student Insights</button>
          </div>

          {activeTab === 'leaderboard' && (
            <div className="grid-2" style={{ marginBottom: '2rem' }}>
              <div className="card glass-card" style={{ flex: 1.5 }}>
                <div className="table-container" style={{ border: 'none' }}>
                  <table>
                    <thead><tr><th>Rank</th><th>Student</th><th>Group</th><th>Commits</th><th>Lines (+/-)</th></tr></thead>
                    <tbody>
                      {dashboardData.leaderboard?.map((s: any, idx: number) => (
                        <tr key={idx}>
                          <td><div className={`rank-badge ${idx < 3 ? 'rank-top' : ''}`}>{idx + 1}</div></td>
                          <td>
                            <div className="student-profile">
                              {s.avatarUrl && <img src={s.avatarUrl} alt="" className="avatar" style={{ border: idx < 3 ? '2px solid #f6d365' : '2px solid var(--border)' }} />}
                              <div><div className="student-name">{s.name} <span className="student-code">({s.studentCode})</span></div>
                                <div className="student-username">@{s.gitHubUsername}</div></div>
                            </div>
                          </td>
                          <td><span className="group-badge">{s.groupName}</span></td>
                          <td><b style={{ fontSize: '1.1rem' }}>{s.commitCount}</b></td>
                          <td><span className="lines-added">+{s.linesAdded}</span> / <span className="lines-deleted">-{s.linesDeleted}</span></td>
                        </tr>
                      ))}
                      {(!dashboardData.leaderboard || dashboardData.leaderboard.length === 0) && <tr><td colSpan={5} className="empty-state">No data. Please sync.</td></tr>}
                    </tbody>
                  </table>
                </div>
              </div>
              
              <div className="card glass-card hover-lift" style={{ flex: 1, display: 'flex', flexDirection: 'column' }}>
                <h3 className="section-title" style={{ fontSize: '1.1rem' }}><PieChartIcon size={20} style={{ color: 'var(--secondary)' }} /> Repository Distribution</h3>
                <div style={{ flex: 1, width: '100%', minHeight: 300 }}>
                  <ResponsiveContainer width="100%" height="100%">
                    <PieChart>
                      <Pie
                        data={repoDistribution}
                        innerRadius={60}
                        outerRadius={80}
                        paddingAngle={5}
                        dataKey="value"
                      >
                        {repoDistribution.map((_: any, index: number) => (
                          <Cell key={`cell-${index}`} fill={COLORS[index % COLORS.length]} />
                        ))}
                      </Pie>
                      <Tooltip 
                        contentStyle={{ backgroundColor: 'var(--card-bg)', border: '1px solid var(--border)', borderRadius: '8px' }}
                      />
                      <Legend verticalAlign="bottom" height={36}/>
                    </PieChart>
                  </ResponsiveContainer>
                </div>
                <p style={{ fontSize: '0.8rem', color: 'var(--text-muted)', textAlign: 'center', marginTop: '1rem' }}>Relative contribution volume per repository</p>
              </div>
            </div>
          )}

          {activeTab === 'students' && (
            <div className="card glass-card" style={{ marginBottom: '2rem' }}>
              {studentInsights.length === 0 ? <div className="empty-state">No student data. Please sync first.</div> : (
                <>
                  <div style={{ padding: '0.5rem 0 2rem 0', borderBottom: '1px solid var(--border)', marginBottom: '1.5rem' }}>
                    <h3 className="section-title" style={{ marginBottom: '1.5rem', fontSize: '1.1rem' }}><BarChartIcon size={20} style={{ color: 'var(--primary)', marginRight: '0.5rem' }} /> Commits by Top Contributors</h3>
                    <div style={{ width: '100%', height: 320 }}>
                      <ResponsiveContainer width="100%" height="100%">
                        <BarChart data={studentInsights.slice(0, 10)}>
                          <CartesianGrid strokeDasharray="3 3" stroke="rgba(255,255,255,0.05)" vertical={false} />
                          <XAxis dataKey="username" stroke="var(--text-muted)" fontSize={12} tickLine={false} axisLine={false} />
                          <YAxis stroke="var(--text-muted)" fontSize={12} tickLine={false} axisLine={false} />
                          <Tooltip 
                            contentStyle={{ backgroundColor: 'var(--card-bg)', border: '1px solid var(--border)', borderRadius: '8px', color: 'var(--text)' }}
                            cursor={{ fill: 'rgba(255,255,255,0.03)' }}
                          />
                          <Legend wrapperStyle={{ paddingTop: '20px' }} />
                          <Bar dataKey="totalCommits" name="Commits" fill="#4f46e5" radius={[4, 4, 0, 0]} barSize={40} />
                        </BarChart>
                      </ResponsiveContainer>
                    </div>
                  </div>
                  <div className="student-insights-grid">
                  {studentInsights.map((si, idx) => (
                    <div className="insight-card" key={idx}>
                      <div style={{ display: 'flex', alignItems: 'center', gap: '0.75rem', marginBottom: '0.75rem' }}>
                        {si.avatar && <img src={si.avatar} alt="" className="avatar" />}
                        <div>
                          <div style={{ fontWeight: 700 }}>{si.username}</div>
                          <div style={{ fontSize: '0.8rem', color: 'var(--text-muted)' }}>{si.totalCommits} commits · <span className="lines-added">+{si.totalAdded}</span> <span className="lines-deleted">-{si.totalDeleted}</span></div>
                        </div>
                      </div>
                      <div style={{ fontSize: '0.75rem', textTransform: 'uppercase', fontWeight: 600, color: 'var(--text-muted)', marginBottom: '0.5rem' }}>Projects</div>
                      {si.repos.map((r: any, ridx: number) => (
                        <div key={ridx} style={{ padding: '0.35rem 0', borderBottom: ridx < si.repos.length - 1 ? '1px solid var(--border)' : 'none', fontSize: '0.85rem' }}>
                          <span style={{ fontWeight: 500 }}>{r.groupName}</span>
                          <span style={{ float: 'right', color: 'var(--text-muted)' }}>{r.commits} commits · <span className="lines-added">+{r.linesAdded}</span> <span className="lines-deleted">-{r.linesDeleted}</span></span>
                        </div>
                      ))}
                    </div>
                  ))}
                  </div>
                </>
              )}
            </div>
          )}

          {/* Collapsible Repositories */}
          <div className="card glass-card" style={{ marginBottom: '2rem' }}>
            <h3 className="section-title"><Code size={20} style={{ color: 'var(--primary)' }} /> Tracking Repositories</h3>
            {dashboardData.repositories?.map((repo: any, idx: number) => {
              const isOpen = expandedRepos[idx];
              const contributorCount = repo.contributors?.length || 0;
              return (
                <div className="collapsible-repo" key={idx}>
                  <div className="collapsible-header" onClick={() => toggleRepo(idx)}>
                    <div style={{ display: 'flex', alignItems: 'center', gap: '0.75rem' }}>
                      {isOpen ? <ChevronDown size={18} /> : <ChevronRight size={18} />}
                      <div>
                        <span style={{ fontWeight: 600 }}>{repo.groupName}</span>
                        <span style={{ marginLeft: '0.75rem', fontSize: '0.8rem', color: 'var(--text-muted)' }}>{contributorCount} contributor{contributorCount !== 1 ? 's' : ''}</span>
                      </div>
                    </div>
                    <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
                      {repo.status === 'Active' ? <span title="Active"><CheckCircle size={16} style={{ color: 'var(--secondary)' }} /></span> : <span title="Error"><AlertCircle size={16} style={{ color: 'var(--danger)' }} /></span>}
                    </div>
                  </div>
                  {isOpen && (
                    <div className="collapsible-body">
                      <a href={repo.repositoryUrl} target="_blank" rel="noreferrer" style={{ fontSize: '0.8rem', color: 'var(--primary)', wordBreak: 'break-all' }}>{repo.repositoryUrl}</a>
                      {repo.lastErrorMessage && <div className="repo-error">{repo.lastErrorMessage}</div>}
                      <div style={{ marginTop: '0.75rem' }}>
                        {repo.contributors?.map((c: any, cidx: number) => (
                          <div className="contributor-item" key={cidx}>
                            {c.avatar && <img src={c.avatar} alt="" className="avatar-sm" />}
                            <div className="contributor-info">
                              <div className="c-name">{c.username} {c.isExternal && <span className="ext-badge">External</span>}</div>
                              <div className="c-stats">{c.commits} commits · <span className="lines-added">+{c.linesAdded}</span> <span className="lines-deleted">-{c.linesDeleted}</span></div>
                            </div>
                          </div>
                        ))}
                      </div>
                    </div>
                  )}
                </div>
              );
            })}
          </div>

          {/* Sync History */}
          <div className="card glass-card">
            <div className="collapsible-header" onClick={() => setShowHistory(!showHistory)} style={{ cursor: 'pointer' }}>
              <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
                {showHistory ? <ChevronDown size={18} /> : <ChevronRight size={18} />}
                <Clock size={20} />
                <h3 style={{ margin: 0 }}>Sync History</h3>
                <span style={{ fontSize: '0.8rem', color: 'var(--text-muted)' }}>({syncHistory.length} records)</span>
              </div>
            </div>
            {showHistory && (
              <div style={{ marginTop: '1rem' }}>
                {syncHistory.length === 0 ? <div className="empty-state">No sync history yet.</div> : (
                  <>
                    <div style={{ width: '100%', height: 260, marginBottom: '2rem', padding: '1rem 0' }}>
                      <ResponsiveContainer width="100%" height="100%">
                        <AreaChart data={[...syncHistory].reverse()}>
                          <defs>
                            <linearGradient id="colorCommits" x1="0" y1="0" x2="0" y2="1">
                              <stop offset="5%" stopColor="#10b981" stopOpacity={0.3}/>
                              <stop offset="95%" stopColor="#10b981" stopOpacity={0}/>
                            </linearGradient>
                          </defs>
                          <CartesianGrid strokeDasharray="3 3" stroke="rgba(255,255,255,0.05)" vertical={false} />
                          <XAxis 
                            dataKey="syncTime" 
                            tickFormatter={(t) => new Date(t).toLocaleDateString([], {month: 'short', day: 'numeric'})} 
                            stroke="var(--text-muted)" fontSize={12} tickLine={false} axisLine={false} 
                          />
                          <YAxis stroke="var(--text-muted)" fontSize={12} tickLine={false} axisLine={false} />
                          <Tooltip 
                            labelFormatter={(t) => new Date(t).toLocaleString()} 
                            contentStyle={{ backgroundColor: 'var(--surface)', border: '1px solid var(--border)', borderRadius: '12px', boxShadow: 'var(--shadow-hover)' }}
                          />
                          <Legend wrapperStyle={{ paddingTop: '10px' }} />
                          <Area type="monotone" dataKey="totalCommits" name="Total Commits" stroke="#10b981" strokeWidth={3} fillOpacity={1} fill="url(#colorCommits)" />
                          <Line type="monotone" dataKey="totalRecords" name="Monitored Repos" stroke="#4f46e5" strokeWidth={2} dot={{ r: 4, fill: '#4f46e5', strokeWidth: 0 }} />
                        </AreaChart>
                      </ResponsiveContainer>
                    </div>
                    <div className="table-container">
                    <table>
                      <thead><tr><th>Batch ID</th><th>Time</th><th>Records</th><th>Total Commits</th><th>Lines (+/-)</th></tr></thead>
                      <tbody>
                        {syncHistory.map((b: any, idx: number) => (
                          <tr key={idx}>
                            <td><span style={{ fontFamily: 'monospace', fontSize: '0.8rem' }}>{b.batchId.substring(0, 8)}...</span></td>
                            <td>{new Date(b.syncTime).toLocaleString()}</td>
                            <td>{b.totalRecords}</td>
                            <td><b>{b.totalCommits}</b></td>
                            <td><span className="lines-added">+{b.totalLinesAdded}</span> / <span className="lines-deleted">-{b.totalLinesDeleted}</span></td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                    </div>
                  </>
                )}
              </div>
            )}
          </div>
        </div>
      ) : (
        <div className="card empty-state" style={{ color: 'var(--danger)' }}>Failed to load dashboard. Ensure the API is running.</div>
      )}

      <style>{`
        .spinner { animation: spin 1s linear infinite; }
        @keyframes spin { 100% { transform: rotate(360deg); } }
        .stats-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(240px, 1fr)); gap: 1.5rem; margin-bottom: 2rem; }
        .grid-2 { display: flex; gap: 1.5rem; flex-wrap: wrap; }
        .stat-card { display: flex; align-items: center; gap: 1rem; padding: 1.5rem; }
        .stat-icon { width: 48px; height: 48px; border-radius: 12px; display: flex; align-items: center; justify-content: center; flex-shrink: 0; }
        .stat-info { display: flex; flex-direction: column; }
        .stat-label { font-size: 0.875rem; color: var(--text-muted); font-weight: 500; }
        .stat-value { font-size: 1.5rem; font-weight: 700; }
        .section-title { display: flex; align-items: center; gap: 0.5rem; margin-bottom: 1rem; font-size: 1.25rem; }
        .rank-badge { display: inline-flex; align-items: center; justify-content: center; width: 28px; height: 28px; border-radius: 50%; background: var(--surface-hover); color: var(--text-muted); font-weight: bold; font-size: 0.875rem; }
        .rank-top { background: linear-gradient(135deg, #f6d365, #fda085); color: #fff; text-shadow: 0 1px 2px rgba(0,0,0,0.2); }
        .group-badge { background: var(--surface-hover); padding: 0.25rem 0.5rem; border-radius: 4px; font-size: 0.8rem; border: 1px solid var(--border); }
        .student-profile { display: flex; align-items: center; gap: 0.75rem; }
        .avatar { width: 40px; height: 40px; border-radius: 50%; border: 2px solid var(--border); object-fit: cover; }
        .avatar-sm { width: 32px; height: 32px; border-radius: 50%; object-fit: cover; }
        .student-name { font-weight: 600; }
        .student-code { color: var(--text-muted); font-weight: normal; font-size: 0.85rem; }
        .student-username { font-size: 0.75rem; color: var(--text-muted); }
        .lines-added { color: var(--secondary); font-weight: 500; }
        .lines-deleted { color: var(--danger); font-weight: 500; }
        .collapsible-repo { border: 1px solid var(--border); border-radius: var(--radius); margin-bottom: 0.75rem; overflow: hidden; }
        .collapsible-header { display: flex; justify-content: space-between; align-items: center; padding: 1rem; cursor: pointer; transition: background 0.15s; }
        .collapsible-header:hover { background: var(--surface-hover); }
        .collapsible-body { padding: 0 1rem 1rem; }
        .contributor-item { display: flex; align-items: center; gap: 0.5rem; margin-bottom: 0.5rem; }
        .contributor-info { display: flex; flex-direction: column; }
        .c-name { font-size: 0.9rem; font-weight: 500; }
        .ext-badge { background: rgba(245,158,11,0.15); color: #d97706; font-size: 0.65rem; padding: 0.15rem 0.35rem; border-radius: 12px; margin-left: 0.25rem; text-transform: uppercase; font-weight: bold; }
        .c-stats { font-size: 0.8rem; color: var(--text-muted); }
        .repo-error { margin-top: 0.5rem; padding: 0.5rem; background: rgba(239,68,68,0.1); color: var(--danger); border-radius: 4px; font-size: 0.8rem; }
        .tab-bar { display: flex; gap: 0; margin-bottom: 1rem; border-bottom: 2px solid var(--border); }
        .tab-btn { padding: 0.75rem 1.25rem; border: none; background: none; cursor: pointer; font-weight: 500; color: var(--text-muted); display: flex; align-items: center; gap: 0.5rem; border-bottom: 2px solid transparent; margin-bottom: -2px; transition: all 0.2s; font-size: 0.9rem; }
        .tab-btn:hover { color: var(--text); background: var(--surface-hover); }
        .tab-active { color: var(--primary) !important; border-bottom-color: var(--primary) !important; font-weight: 600; }
        .student-insights-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(320px, 1fr)); gap: 1rem; }
        .insight-card { padding: 1rem; border: 1px solid var(--border); border-radius: var(--radius); background: var(--surface-hover); }
        .skeleton-loader { width: 60%; height: 12px; background: linear-gradient(90deg, var(--surface-hover) 25%, var(--border) 50%, var(--surface-hover) 75%); background-size: 200% 100%; animation: shimmer 1.5s infinite; border-radius: 4px; margin-bottom: 0.5rem; }
        @keyframes shimmer { 0% { background-position: 200% 0; } 100% { background-position: -200% 0; } }
      `}</style>
    </div>
  );
};
