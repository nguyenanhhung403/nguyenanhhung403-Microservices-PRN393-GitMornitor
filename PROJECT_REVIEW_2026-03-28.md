# GitMonitor Project Review (2026-03-28)

Author: GitHub Copilot (GPT-5.3-Codex)
Scope: Full repository review after fresh pull
Repository: nguyenanhhung403-Microservices-PRN393-GitMornitor

---

## 1. Executive Summary

GitMonitor is a classroom-oriented platform for tracking student GitHub activity in a structured, teacher-centric workflow.

At a high level, the system is split into:

- Frontend Web App (React + TypeScript + Vite)
- API Gateway (YARP reverse proxy)
- Identity Service (teacher management)
- Classroom Service (class/group/student management)
- Monitoring Service (GitHub sync + analytics)
- Legacy monolith in FinalProject (kept for reference/migration)

The current implementation favors development speed and demo readiness over enterprise hardening.

Strengths:

- Clear bounded contexts (Identity, Classroom, Monitoring)
- Fast startup and simple local/deploy flow
- Useful dashboard and contribution analytics
- Swagger available for each service and aggregated through gateway

Weaknesses:

- No auth/authz at API level
- Shared database across services (tight coupling)
- No test projects currently present
- No robust observability stack (metrics/traces)
- Token handling security needs improvement

---

## 2. What The Project Does

Main business objective:

- Help teachers manage classrooms and groups
- Bind each group to a GitHub repository
- Sync contribution data from GitHub
- Render leaderboard and per-student/per-repo analytics

Practical workflow:

1. Teacher account is created
2. Teacher creates classroom
3. Students are imported into groups (optionally via JSON bulk import)
4. GitHub token is configured (classroom/group level)
5. Monitoring API syncs data from GitHub
6. Dashboard visualizes commits/lines and history

Who uses it:

- Teacher or classroom admin

Who is tracked:

- Students mapped by student code and GitHub username

Outputs:

- Leaderboard by commits
- Code churn metrics (+/- lines)
- Sync history batches
- Repo health/status indicators

---

## 3. Repository Map

Top-level folders/files and intent:

- ClientApp/
  - React frontend
- src/
  - Main microservices architecture
- FinalProject/
  - Older monolithic implementation
- docker-compose.yml
  - Multi-container orchestration
- start-services.ps1
  - Local startup script (separate windows)
- GitMonitor.db.sql
  - SQL schema bootstrap/reference
- GitMonitor.sln / GitMonitor.slnx
  - Solution files

Notes:

- Repo contains both current microservices and legacy monolith.
- This is useful for comparing migration direction but can confuse newcomers without docs.

---

## 4. Architecture Overview

### 4.1 Logical Architecture

```text
ClientApp (React)
   |
   v
ApiGateway (YARP)
   |---------------------------> Identity.API
   |---------------------------> Classroom.API
   |---------------------------> Monitoring.API

All services currently use the same SQLite database file.
```

### 4.2 Service Responsibilities

Identity.API:

- CRUD teachers
- Store optional default GitHub token per teacher

Classroom.API:

- CRUD classrooms
- Manage student groups
- Import and manage students
- Configure group/class tokens

Monitoring.API:

- Trigger sync for classroom or group
- Query GitHub (REST + GraphQL)
- Build dashboard response
- Persist sync history

ApiGateway:

- Single API entrypoint for frontend
- Route by path prefix
- Proxy service Swagger docs to unified UI

### 4.3 Legacy Component

FinalProject/GitStudentMonitorApi:

- Earlier all-in-one API
- Includes background auto-sync worker
- Useful as reference for design decisions and migrated logic

---

## 5. Runtime Topology

### 5.1 Ports

- Gateway: 5000
- Identity.API: 5051
- Classroom.API: 5020
- Monitoring.API: 5039
- ClientApp (nginx): 5173

### 5.2 Deployment Modes

Local mode:

- Run each API using dotnet run
- Use start-services.ps1 to open dedicated terminals

Docker mode:

- Use docker-compose up --build
- All services are containerized

### 5.3 Configuration Traits

- Default environment is Development
- Gateway cluster destinations configurable via environment variables
- Database connection is SQLite file path in each service config

---

## 6. API Gateway Review

Location:

- src/ApiGateway

Key behaviors:

- Uses YARP reverse proxy
- CORS policy currently allow-all
- Swagger endpoints exposed at gateway root
- Routes split by api path prefixes

Route patterns covered:

- /api/teachers/* -> Identity
- /api/classrooms/* -> Classroom
- /api/students/* -> Classroom
- /api/dashboard/* -> Monitoring
- /api/sync/* -> Monitoring
- /api/sync-history/* -> Monitoring

Benefits:

- Frontend has one stable base URL
- Easy service replacement behind gateway

Trade-offs:

- No gateway auth/rate-limit policies yet
- No request correlation middleware currently configured

---

## 7. Identity.API Review

Location:

- src/Services/Identity.API

Role:

- Teacher identity profile management (business identity, not authentication identity)

Core components:

- Program.cs (service registration + pipeline)
- Data/IdentityDbContext.cs
- Entities/Teacher.cs
- DTOs/TeacherDtos.cs
- Endpoints/TeacherEndpoints.cs

API coverage:

- GET /api/teachers
- GET /api/teachers/{id}
- POST /api/teachers
- PUT /api/teachers/{id}
- DELETE /api/teachers/{id}

Validation patterns seen:

- Unique username checks in endpoint logic
- Basic not-found handling

Current gap:

- No login/session/JWT concerns in this service
- It acts as teacher directory rather than auth provider

---

## 8. Classroom.API Review

Location:

- src/Services/Classroom.API

Role:

- Manage classroom structure and student roster

Core components:

- Program.cs
- Data/ClassroomDbContext.cs
- Entities/Models.cs
- DTOs/ClassroomDtos.cs
- Endpoints/ClassroomEndpoints.cs
- Endpoints/StudentEndpoints.cs
- Enums/GroupStatus.cs

Domain entities:

- ClassRoom
- StudentGroup
- Student

Endpoint highlights:

Classroom endpoints:

- GET /api/classrooms
- GET /api/classrooms/{id}
- POST /api/classrooms
- PUT /api/classrooms/{id}
- DELETE /api/classrooms/{id}
- PUT /api/classrooms/{classRoomId}/token
- POST /api/classrooms/{classRoomId}/import
- DELETE /api/classrooms/{classRoomId}/students/{studentId}

Student endpoints:

- GET /api/students
- GET /api/students/{id}
- PUT /api/students/{id}
- DELETE /api/students/{id}

Business value:

- Central operational CRUD for class administration
- Bulk import lowers onboarding friction

Risks:

- TeacherId trust boundary appears weak (cross-service validation absent)
- No authorization checks for who can modify which class

---

## 9. Monitoring.API Review

Location:

- src/Services/Monitoring.API

Role:

- Sync GitHub data and return analytics dashboard

Core components:

- Program.cs
- Data/MonitoringDbContext.cs
- Services/SyncService.cs
- Services/GitHub/IGitHubApi.cs
- Services/GitHub/GitHubApiService.cs
- Services/GitHub/GitHubAuthHandler.cs
- Services/GitHub/GitHubTokenProvider.cs
- Endpoints/SyncEndpoints.cs
- DTOs/MonitoringDtos.cs
- Entities/Models.cs

API endpoints:

- GET /api/dashboard/{classRoomId}
- POST /api/sync/{classRoomId}
- POST /api/sync/group/{groupId}
- GET /api/sync-history/{classRoomId}

Observed design decisions:

- Refit client for GitHub API
- Custom delegating handler for auth token injection
- Fallback behavior when token fails for public repos
- Batch history persisted for trend and traceability

Positive implementation detail:

- Explicit timeout and user-agent configuration for GitHub calls

Technical debt:

- Manual SQL patch for schema drift (ALTER TABLE in startup)
- EnsureCreated used instead of migration workflow

---

## 10. Data Model Review

Current data ownership is service-oriented in code but physically shared in one SQLite database.

Main tables and intent:

- Teachers: teacher profile and default token
- ClassRooms: class metadata and owner (teacher id)
- StudentGroups: group + repository + token + status
- Students: student identities and group relation
- SyncHistory: historical contribution snapshot per sync

Core relationships:

- Teacher 1..N ClassRooms
- ClassRoom 1..N StudentGroups
- StudentGroup 1..N Students
- Student 1..N SyncHistory

Design strength:

- Simple and easy to operate in low scale environments

Design risk:

- Shared DB undermines strict microservice isolation
- Any schema change can impact all services simultaneously

---

## 11. Frontend Review (ClientApp)

Location:

- ClientApp/src

Stack:

- React 19
- TypeScript
- Axios
- Recharts
- Lucide icons

Main pages:

- Home.tsx (dashboard)
- Classrooms.tsx
- ClassroomDetail.tsx
- Teachers.tsx

Important frontend behavior:

- Uses /api as base URL (gateway-first strategy)
- Stores selected classroom in localStorage
- Supports manual sync trigger
- Visuals include leaderboard, distribution chart, history

Strengths:

- Functional dashboard UX for classroom use case
- Good chart coverage for contribution summary

Potential improvements:

- Introduce typed API contracts and stricter DTO typing
- Add query cache/state manager if data grows
- Add loading/error boundaries per panel

---

## 12. Legacy Monolith Review (FinalProject)

Location:

- FinalProject/GitStudentMonitorApi

What it represents:

- Original integrated API prior to service decomposition

Why it matters:

- Good historical reference for moved logic
- Useful when validating behavior parity between old/new stack

Key distinction from current microservices:

- Monolith had hosted worker in same app
- New stack separates responsibilities but still uses shared DB

Recommendation:

- Keep legacy folder read-only and clearly documented as archived reference
- Avoid accidental edits unless migration work is active

---

## 13. Security Review

Current status:

- No JWT/OAuth/API-key enforcement on services
- CORS is broad allow-all
- GitHub tokens appear stored in plain DB fields

Primary risks:

- Unauthorized write operations possible
- Token leakage risk if DB exposed
- No per-teacher tenancy enforcement

Suggested hardening sequence:

1. Add authentication middleware (JWT bearer)
2. Add authorization policy by teacher ownership
3. Encrypt GitHub tokens at rest
4. Restrict CORS by known frontend origin
5. Add request throttling on sync endpoints

---

## 14. Reliability And Observability Review

Current reliability posture:

- Basic exception handling present in endpoint-level try/catch
- Limited structured logging
- No health probes currently visible

What is missing for production:

- Liveness/readiness health checks
- Centralized structured logs
- Trace IDs across gateway and services
- Metrics for sync duration/error rate

Observability roadmap:

1. Add /health endpoints in each service
2. Add OpenTelemetry tracing
3. Add correlation-id middleware at gateway
4. Export metrics for sync jobs
5. Dashboard key SLOs (sync success ratio, p95 sync time)

---

## 15. Code Quality And Testing Review

Current state:

- No dedicated test projects found in workspace structure
- Endpoint behavior mostly validated by manual/API calls

Implications:

- Refactor confidence is low
- Regression risk high when changing sync logic

Suggested test pyramid:

- Unit tests for parsing and metrics aggregation
- Integration tests for endpoint contracts
- Lightweight E2E smoke test through gateway

Priority candidates for tests:

- SyncService repository parsing and aggregation
- Classroom import mapping logic
- Teacher uniqueness behavior
- Gateway route pass-through

---

## 16. Architecture Verdict

Is this really microservices?

Short answer:

- Functional separation exists at process and code level
- Data and security boundaries are still monolith-like

Current maturity classification:

- Early-stage microservices transition architecture

Good for:

- Team learning microservices decomposition
- Classroom demos and small-scale deployments

Not yet ready for:

- Multi-tenant production workloads
- Strict compliance and security requirements

---

## 17. Contribution Plan (~1000 LOC)

This section proposes one practical contribution package near 1000 lines of code.

Goal:

- Deliver measurable architecture improvement with moderate scope

Theme:

- Add authentication + ownership authorization + health checks + basic tests

### 17.1 Proposed Breakdown

Task A: JWT Authentication Foundation (~220 LOC)

- Add auth settings model and binding
- Add JWT bearer middleware in gateway/services
- Add token validation configuration
- Add authentication requirement for mutating endpoints

Task B: Teacher Ownership Authorization (~180 LOC)

- Add teacher claim extraction helper
- Add policy check on classroom mutation endpoints
- Add ownership guard responses (403)

Task C: Token Security Layer (~120 LOC)

- Add token protection service abstraction
- Encrypt/decrypt GitHub token before persistence
- Migrate existing token reads/writes via service

Task D: Health Checks And Diagnostic Endpoints (~140 LOC)

- Add ASP.NET health checks in 4 services
- Add DB connectivity checks
- Expose standard /health route

Task E: Observability Starter (~110 LOC)

- Add correlation id middleware in gateway
- Log request id on downstream calls
- Basic structured logs for sync start/end/error

Task F: Integration Tests Starter (~240 LOC)

- Create test project
- Add minimal test host setup
- Add 5-8 integration tests for critical routes

Estimated total: 1010 LOC

### 17.2 Deliverables

- Secure mutation endpoints
- Ownership-safe classroom operations
- Encrypted token storage flow
- Health endpoints for orchestration
- Initial confidence tests

### 17.3 Suggested Commit Plan

1. feat(auth): add jwt bearer base wiring
2. feat(authz): enforce teacher ownership on classroom writes
3. feat(security): encrypt github tokens at rest
4. feat(ops): add health checks and diagnostics
5. test(api): add integration smoke tests

---

## 18. Contribution Plan (Alternative 1000 LOC)

If auth is deferred, alternative package:

Theme:

- Improve monitoring depth and sync quality

Breakdown:

- Detailed per-commit ingestion pipeline: 250 LOC
- Sync job retry and transient-fault policy: 180 LOC
- Rich dashboard filters/date ranges: 210 LOC
- Pagination and search in students/classrooms: 140 LOC
- Export CSV for leaderboard/history: 120 LOC
- Tests for sync and export flows: 130 LOC

Estimated total: 1030 LOC

---

## 19. Suggested Next Milestones

Milestone 1 (1-2 days):

- Add health checks + correlation id + basic logs

Milestone 2 (2-3 days):

- Add JWT authentication and teacher ownership checks

Milestone 3 (2-3 days):

- Add integration tests for gateway + core APIs

Milestone 4 (1-2 days):

- Harden token handling and config management

---

## 20. Runbook For New Contributor

Prerequisites:

- .NET SDK compatible with project target
- Node.js and npm
- Docker Desktop (optional)

Quick local startup:

1. Run start-services.ps1
2. Run frontend dev server (ClientApp)
3. Open gateway swagger on port 5000
4. Create teacher, classroom, import students
5. Trigger sync and view dashboard

Quick docker startup:

1. docker-compose up --build
2. Open client on port 5173
3. Use gateway APIs through frontend

---

## 21. Gaps To Document In README

Important documentation currently missing or light:

- Clear project vision and architecture diagram
- DB schema ownership and migration policy
- Security model and non-goals
- Troubleshooting for GitHub rate limits
- Contribution standards and branch naming

Recommended docs to add:

- docs/architecture.md
- docs/run-local.md
- docs/run-docker.md
- docs/security.md
- CONTRIBUTING.md

---

## 22. Final Review Conclusion

This repository already delivers meaningful classroom value and has a clear decomposition intent.

The most impactful next step is not feature sprawl, but hardening:

- authenticate requests
- enforce ownership boundaries
- improve test coverage
- improve observability

If those four are delivered in the proposed ~1000 LOC contribution, the project quality will move from demo-grade to a much stronger engineering baseline.

---

## 23. Verified Reference Files Used In This Review

Microservices:

- src/ApiGateway/Program.cs
- src/ApiGateway/appsettings.json
- src/Services/Identity.API/Program.cs
- src/Services/Identity.API/Endpoints/TeacherEndpoints.cs
- src/Services/Identity.API/Entities/Teacher.cs
- src/Services/Classroom.API/Program.cs
- src/Services/Classroom.API/Endpoints/ClassroomEndpoints.cs
- src/Services/Classroom.API/Endpoints/StudentEndpoints.cs
- src/Services/Classroom.API/Entities/Models.cs
- src/Services/Classroom.API/DTOs/ClassroomDtos.cs
- src/Services/Monitoring.API/Program.cs
- src/Services/Monitoring.API/Endpoints/SyncEndpoints.cs
- src/Services/Monitoring.API/Services/SyncService.cs
- src/Services/Monitoring.API/Services/GitHub/IGitHubApi.cs
- src/Services/Monitoring.API/Services/GitHub/GitHubApiService.cs
- src/Services/Monitoring.API/Entities/Models.cs
- src/Services/Monitoring.API/DTOs/MonitoringDtos.cs

Frontend:

- ClientApp/src/services/api.ts
- ClientApp/src/pages/Home.tsx

Infra:

- docker-compose.yml
- start-services.ps1
- GitMonitor.db.sql

Legacy reference:

- FinalProject/GitStudentMonitorApi/Program.cs
- FinalProject/GitStudentMonitorApi/Endpoints/TeacherEndpoints.cs
- FinalProject/GitStudentMonitorApi/Endpoints/ClassroomEndpoints.cs
- FinalProject/GitStudentMonitorApi/Endpoints/StudentEndpoints.cs
- FinalProject/GitStudentMonitorApi/Endpoints/SyncEndpoints.cs

---

## 24. Optional Actionable Ticket List

Ticket 1:

- Title: Add JWT auth baseline
- Type: feature
- Priority: high
- Estimate: 1.5 days

Ticket 2:

- Title: Enforce teacher ownership in Classroom API
- Type: feature
- Priority: high
- Estimate: 1 day

Ticket 3:

- Title: Add health checks and readiness endpoints
- Type: ops
- Priority: medium
- Estimate: 0.5 day

Ticket 4:

- Title: Add integration tests for key endpoints
- Type: test
- Priority: high
- Estimate: 2 days

Ticket 5:

- Title: Secure GitHub token storage
- Type: security
- Priority: high
- Estimate: 1 day

Ticket 6:

- Title: Add tracing and request correlation
- Type: observability
- Priority: medium
- Estimate: 1 day

---

## 25. Closing

Review file created on a dedicated branch.

Suggested branch for this work:

- docs/project-review-architecture-20260328

You can now commit this file as a standalone documentation contribution or continue by implementing the 1000 LOC plan from section 17.
