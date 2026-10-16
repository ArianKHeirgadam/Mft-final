# API endpoint contract

Base URL: `/api`. All routes except login, health and the explicit password lifecycle routes require a valid JWT. Role checks must be enforced in controllers/policies, not only in the frontend.

## Authentication

| Method | Route | Access | Purpose |
|---|---|---|---|
| POST | `/auth/login` | Anonymous | Authenticate username/password; return access token and role |
| GET | `/auth/me` | Any authenticated user | Current identity |
| POST | `/auth/change-password` | Authenticated user | Change temporary/current password |
| POST | `/auth/logout` | Authenticated user (optional for stateless JWT) | Client-side token discard / optional revocation |
| GET | `/health` | Anonymous | Health check |

## Users and roles

| Method | Route | Allowed roles | Purpose |
|---|---|---|---|
| GET | `/users` | SuperAdmin, Department, Teacher (scope-filtered) | List visible users |
| GET | `/users/{id}` | SuperAdmin, Department, Teacher (scope-filtered) | Get user |
| POST | `/users` | SuperAdmin, Department, Teacher (role-restricted) | Create account with generated temporary password |
| PUT | `/users/{id}` | SuperAdmin, Department (own department), Teacher (own students only) | Update permitted account fields |
| PATCH | `/users/{id}/active` | SuperAdmin, Department (own department) | Activate/deactivate account |
| POST | `/users/{id}/reset-password` | SuperAdmin, Department (own department), Teacher (own students only) | Generate temporary password and require change |
| DELETE | `/users/{id}` | SuperAdmin only | Delete/deactivate account under referential-integrity policy |

A caller cannot assign a role higher than their own. Department users cannot cross department boundaries. Teachers may create/manage students only.

## Departments

| Method | Route | Allowed roles | Purpose |
|---|---|---|---|
| GET | `/departments` | Any authenticated user (scope-limited) | List departments |
| GET | `/departments/{id}` | Any authenticated user (scope-limited) | Department detail |
| POST | `/departments` | SuperAdmin | Create department and optionally assign department manager |
| PUT | `/departments/{id}` | SuperAdmin | Update department |
| DELETE | `/departments/{id}` | SuperAdmin | Delete only if no dependent records, otherwise return 409 |

## Courses / subjects

The Courses entity and routes are implemented. Apply the additive script `database/004_courses.sql` to `MftDb` before using these endpoints; the API does not run schema changes automatically.

| Method | Route | Allowed roles | Purpose |
|---|---|---|---|
| GET | `/courses?departmentId={id}` | Authenticated user (scope-limited) | List visible courses |
| GET | `/courses/{id}` | Authenticated user (scope-limited) | Course detail |
| POST | `/courses` | SuperAdmin, Department | Create course within permitted department |
| PUT | `/courses/{id}` | SuperAdmin, Department (own department) | Update course |
| DELETE | `/courses/{id}` | SuperAdmin, Department (own department) | Delete only if not referenced, otherwise 409 |

## Questions and answer keys

| Method | Route | Allowed roles | Purpose |
|---|---|---|---|
| GET | `/questions` | SuperAdmin, Department, Teacher (scope-limited) | List question bank |
| GET | `/questions/{id}` | SuperAdmin, Department, owning Teacher | Get question |
| POST | `/questions` | SuperAdmin, Department, Teacher | Create question/options |
| PUT | `/questions/{id}` | SuperAdmin, Department (own department), owning Teacher | Update question/options |
| DELETE | `/questions/{id}` | SuperAdmin, Department (own department), owning Teacher | Delete if not used by exams |
| GET | `/answer-keys` | SuperAdmin, Department, Teacher (scope-limited) | List answer keys |
| POST | `/answer-keys` | SuperAdmin, Department, Teacher | Create/upload answer key; teacher limited to own course/department |
| GET | `/answer-keys/{id}` | SuperAdmin, Department, Teacher (scope-limited) | Retrieve answer key metadata/content |
| PUT | `/answer-keys/{id}` | SuperAdmin, Department (own department), owning Teacher | Replace/update key |
| DELETE | `/answer-keys/{id}` | SuperAdmin, Department (own department), owning Teacher | Delete key if unused |

Correct answers and answer-key data must never be included in a student's exam payload.

## Exams and attempts

| Method | Route | Allowed roles | Purpose |
|---|---|---|---|
| GET | `/exams` | All authenticated roles (scope-limited) | List available exams |
| GET | `/exams/{id}` | All authenticated roles (student-safe projection) | Exam detail without answer key |
| POST | `/exams` | SuperAdmin, Department, Teacher | Create exam; teacher limited to own department |
| PUT | `/exams/{id}` | SuperAdmin, Department (own department), owning Teacher | Update exam |
| POST | `/exams/{id}/publish` | SuperAdmin, Department (own department), owning Teacher | Publish/schedule exam |
| DELETE | `/exams/{id}` | SuperAdmin, Department (own department), owning Teacher | Delete only if no attempts exist |
| POST | `/exams/{id}/attempts` | Student | Start an eligible exam |
| GET | `/attempts/{id}` | Student (own attempt), staff (scope-limited) | Get attempt; student projection only |
| PUT | `/attempts/{id}/answers` | Student (own in-progress attempt) | Save answer(s) |
| POST | `/attempts/{id}/submit` | Student (own in-progress attempt) | Submit exam; server validates and scores objective questions |
| GET | `/exams/{id}/results` | SuperAdmin, Department, owning Teacher | Results for exam |
| GET | `/students/me/results` | Student | Own results only |

Students must never be allowed to submit arbitrary scores, change another student's attempt, or read correct answers before submission/release.


## Routes implemented in this repository

- `POST /api/auth/login`, `POST /api/auth/change-password`
- `GET /api/health`
- `GET|POST /api/students`, `DELETE /api/students/{id}`
- `GET|POST /api/teachers`
- `POST /api/department-managers`
- `GET|POST /api/departments`, `PUT|DELETE /api/departments/{id}`
- `GET|POST /api/questions`
- `GET|POST /api/exams`
- `GET|POST /api/courses`, `GET|PUT|DELETE /api/courses/{id}`
- `GET|POST /api/answer-keys`, `GET|PUT|DELETE /api/answer-keys/{id}`
- `POST /api/answer-keys/import` (multipart upload: XLSX, XLSM, text-based PDF, CSV, TSV, JSON, XML, TXT)
- `GET /api/portal/me`, `GET /api/portal/my-exams`, `POST /api/portal/exams/{examId}/start`
- `POST /api/portal/attempts/{attemptId}/submit`, `GET /api/portal/my-attempts`
- `GET /api/portal/notifications`, `POST /api/portal/notifications/{id}/read`
- `PUT /api/manage/students/{id}`, `PUT /api/manage/teachers/{id}`
- `PATCH /api/manage/users/{id}/active`, `POST /api/manage/users/{id}/reset-password`
- `PUT|DELETE /api/manage/questions/{id}`, `PUT|DELETE /api/manage/exams/{id}`
- `GET /api/manage/exams/{id}/attempts`, `PUT /api/manage/answers/{id}/grade`

Objective questions are automatically scored and normalized to 100. Essay questions remain pending manual grading. Correct options are not returned in student exam payloads.


## Answer-key import formats

The upload endpoint accepts a multipart form with fields `DepartmentId`, `TeacherId`, and `File` (maximum 10 MB, 500 questions per import). Teachers may import only for their own teacher profile and department.

- **XLSX/XLSM, CSV, TSV:** first row/line is a header with `Text`, and optional `Type`, `Points`, `Options` columns. `Options` format: `Option A|false;Option B|true`.
- **JSON:** one question object or an array, e.g. `{"text":"2+2?","type":"MultipleChoice","points":1,"options":[{"text":"4","isCorrect":true},{"text":"5","isCorrect":false}]}`.
- **XML:** `<Questions><Question><Text>...</Text><Type>MultipleChoice</Type><Points>1</Points><Options><Option IsCorrect="true">4</Option><Option IsCorrect="false">5</Option></Options></Question></Questions>`.
- **TXT:** tab-delimited rows using the same header format, or JSON content.
- **PDF:** text-based PDFs must contain one question per line: `Question text | MultipleChoice | 1 | Option A:false;Option B:true`. Scanned/image-only PDFs need OCR before upload.

Multiple-choice questions require at least two options and exactly one correct option. Student-facing exam responses never include the correct-option flags.
