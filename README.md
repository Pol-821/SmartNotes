# SmartNotes

**Autor:** Pol821 (Pol Mira i Fernández)
**Copyright:** © 2026 Pol Mira i Fernández. Tots els drets reservats.

> Aquest software és propietat intel·lectual del seu autor. No està permès el seu ús comercial, distribució o modificació sense autorització explícita.

---

## Visió General

SmartNotes és un sistema complet de transcripció i resum de classes amb IA. Consta de tres components:

- **Backend** (`SmartNotes.Api`): API REST en ASP.NET Core 8.0
- **Frontend** (`smartnotes-web`): SPA en React + TypeScript + Vite
- **Client Raspberry Pi** (`SmartNotes.Raspberry`): Client Python per gravar àudio i pujar-lo automàticament

L'arquitectura segueix el patró: **Raspberry Pi** → **API (R2 + PostgreSQL)** → **Worker (Whisper + Groq/Llama)** → **Frontend React**

---

## Backend — SmartNotes.Api

API REST en ASP.NET Core 8.0 amb PostgreSQL, Cloudflare R2 (S3-compatible) i integració amb Groq AI.

### Estructura

```
SmartNotes.Api/
├── Controllers/           # 7 controllers REST
│   ├── AuthController.cs        # Registre, login, refresh, logout, reset password
│   ├── ClassroomController.cs   # CRUD aules, matrícules alumnes
│   ├── NotesController.cs       # CRUD apunts, upload àudio, streaming
│   ├── RaspberryController.cs   # Provisionament dispositius, upload Raspberry
│   ├── SubscriptionController.cs# Plans i subscripcions
│   ├── TranscriptionController.cs# Transcripcions, històric, reintents
│   └── UserController.cs        # Perfil, settings, sessions
│
├── Services/              # Lògica de negoci
│   ├── AI/                     # Serveis d'intel·ligència artificial
│   │   ├── GroqClient.cs            # Client per Groq Chat (Llama)
│   │   ├── GroqAudioClient.cs       # Client per Groq Audio (Whisper)
│   │   └── SmartNotesEngine.cs      # Orquestració: split text + resum per chunks
│   ├── AudioPreprocessor.cs    # Neteja i millora d'àudio amb ffmpeg
│   ├── ClassroomService.cs     # Lògica d'aules i enròlments
│   ├── EmailService.cs         # Enviament d'emails SMTP (Gmail)
│   ├── FfmpegRunner.cs         # Abstracció per execució de ffmpeg/ffprobe
│   ├── JwtService.cs           # Generació i validació de JWT
│   ├── MimeTypeHelper.cs       # Detecció de MIME types
│   ├── NoteService.cs          # Lògica CRUD d'apunts
│   ├── R2Service.cs            # Cloudflare R2 (S3) per emmagatzematge d'àudio
│   ├── TranscriptionQueue.cs   # Cua en memòria per treballs de transcripció
│   ├── TranscriptionStore.cs   # Magatzem en memòria per progrés de treballs
│   ├── TranscriptionWorker.cs  # Background service: processa la cua
│   ├── UserService.cs          # Lògica d'usuaris, autenticació, seguretat
│   └── WhisperService.cs       # Wrapper sobre GroqAudioClient
│
├── Models/                # 12 models EF Core
│   ├── Classroom.cs, Enrollment.cs, Note.cs
│   ├── RaspberryDevice.cs, RefreshToken.cs
│   ├── SmartSummary.cs, SubscriptionPlan.cs
│   ├── TranscriptionJob.cs, TranscriptionRecord.cs, TranscriptionStatus.cs
│   ├── User.cs, UserSubscription.cs
│
├── DTOs/                  # 9 DTOs amb validació
├── Data/
│   └── SmartNotesDbContext.cs    # DbContext EF Core
├── Extensions/
│   └── ClaimsExtensions.cs       # Extensió per extreure userId del JWT
├── Mappers/
│   └── TranscriptionMapper.cs    # Mapeig Job → DTO
└── Migrations/            # Migracions EF Core
```

### API Endpoints

| Mètode | Endpoint | Autenticació | Descripció |
|--------|----------|-------------|------------|
| POST | `/api/auth/register` | No | Registre d'usuari |
| POST | `/api/auth/login` | No | Inici de sessió |
| POST | `/api/auth/refresh` | No | Refrescar JWT |
| POST | `/api/auth/forgot-password` | No | Sol·licitar reset de contrasenya |
| POST | `/api/auth/reset-password` | No | Reset de contrasenya amb token |
| POST | `/api/auth/logout` | Sí | Tancar sessió |
| GET | `/api/notes` | Sí | Llistar apunts (paginat) |
| GET | `/api/notes/{id}` | Sí | Detall d'un apunt |
| POST | `/api/notes` | Sí | Crear apunt de text (300s) |
| PUT | `/api/notes/{id}` | Sí | Actualitzar apunt |
| DELETE | `/api/notes/{id}` | Sí | Eliminar apunt |
| POST | `/api/notes/upload` | Sí | Pujar àudio (transcripció automàtica) |
| GET | `/api/notes/{id}/audio` | Sí | Stream d'àudio millorat |
| PUT | `/api/notes/{id}/move` | Sí | Moure apunt a aula |
| PATCH | `/api/notes/{id}/public` | Sí | Fer apunt públic/compartit |
| GET | `/api/notes/shared/{publicId}` | No | Veure apunt compartit |
| GET | `/api/classroom` | Sí | Llistar aules (professor) |
| POST | `/api/classroom` | Sí | Crear aula |
| DELETE | `/api/classroom/{id}` | Sí | Eliminar aula |
| POST | `/api/classroom/join` | Sí | Unir-se a aula (alumne) |
| GET | `/api/classroom/enrolled` | Sí | Aules matriculades (alumne) |
| GET | `/api/classroom/{id}/notes` | Sí | Apunts d'una aula |
| GET | `/api/classroom/{id}/students` | Sí | Alumnes d'una aula |
| DELETE | `/api/classroom/{id}/students/{studentId}` | Sí | Expulsar alumne |
| GET | `/api/classroom/note/{noteId}` | Sí | Veure apunt com a alumne |
| GET | `/api/subscription/plans` | No | Llistar plans |
| GET | `/api/subscription/my-subscription` | Sí | Subscripció actual |
| POST | `/api/subscription/subscribe` | Sí | Subscriure's a pla |
| POST | `/api/subscription/cancel` | Sí | Cancel·lar subscripció |
| GET | `/api/user/me` | Sí | Perfil d'usuari |
| PUT | `/api/user/settings` | Sí | Actualitzar preferències |
| GET | `/api/user/sessions` | Sí | Sessions actives |
| POST | `/api/user/sessions/{id}/revoke` | Sí | Revocar sessió |
| POST | `/api/transcription` | Sí | Transcriure àudio |
| GET | `/api/transcription/{id}` | Sí | Estat d'un job |
| POST | `/api/transcription/{id}/cancel` | Sí | Cancel·lar transcripció |
| POST | `/api/transcription/{id}/retry` | Sí | Reintentar amb altre idioma |
| GET | `/api/transcription/active` | Sí | Jobs actius |
| GET | `/api/transcription/history` | Sí | Històric de transcripcions |
| DELETE | `/api/transcription/record/{id}` | Sí | Eliminar registre |
| GET | `/api/raspberry/devices` | Sí | Llistar dispositius |
| POST | `/api/raspberry/provision` | Sí | Provisionar dispositiu nou |
| DELETE | `/api/raspberry/devices/{id}` | Sí | Desvincular dispositiu |
| POST | `/api/raspberry/register` | No | Registrar-se com a Raspberry |
| GET | `/api/raspberry/check/{serial}` | No | Comprovar estat dispositiu |

### Tecnologies Backend

- **.NET 8.0** amb ASP.NET Core minimal API
- **Entity Framework Core 8.0** amb **Npgsql** (PostgreSQL a Neon.tech)
- **JWT Bearer** per autenticació (refresh tokens persistits)
- **BCrypt.Net-Next** per hash de contrasenyes
- **Cloudflare R2** (S3 compatible) per àudio
- **Groq API**: `whisper-large-v3` (transcripció) + `llama-3.3-70b-versatile` (resum)
- **ffmpeg/ffprobe** per processament d'àudio
- **AspNetCoreRateLimit** per protecció contra bruteforce als endpoints d'auth
- **Swashbuckle** (Swagger) per documentació OpenAPI

---

## Frontend — smartnotes-web

SPA en React 18 + TypeScript + Vite amb Tailwind CSS i components Radix UI.

### Estructura

```
smartnotes-web/src/
├── components/              # Components reutilitzables
│   ├── layout/
│   │   └── MainLayout.tsx       # Layout principal amb sidebar i capçalera
│   ├── ui/                      # Components UI (shadcn/ui)
│   │   ├── button.tsx, card.tsx, dialog.tsx, input.tsx, label.tsx, spinner.tsx, sonner.tsx
│   ├── ActiveJobsWidget.tsx     # Widget de treballs de transcripció en curs
│   ├── AudioPlayer.tsx          # Reproductor d'àudio amb Web Audio API
│   ├── ConfirmDialog.tsx        # Diàleg de confirmació (basat en Radix Dialog)
│   ├── ErrorBoundary.tsx        # Captura d'errors de render
│   ├── NoteCard.tsx             # Targeta d'apunt (reutilitzada a 3 pantalles)
│   └── RescueModal.tsx          # Modal de rescat per reintents
│
├── contexts/                # Contextos React
│   └── UserContext.tsx          # Context de perfil d'usuari (fetched una vegada)
│
├── lib/                     # Utilitats
│   ├── auth.ts                  # Funcions d'autenticació (token, expiry, clear)
│   ├── constants.ts             # Constants globals (intervals, paginació)
│   └── utils.ts                 # Utilitats generals (cn, formateig)
│
├── pages/                   # 14 pàgines (càrrega lazy)
│   ├── LandingScreen.tsx        # Pàgina d'inici (pública)
│   ├── LoginScreen.tsx          # Login
│   ├── RegisterScreen.tsx       # Registre amb rol i idiomes
│   ├── ForgotPasswordScreen.tsx # Recuperació de contrasenya
│   ├── DashboardScreen.tsx      # Dashboard professor (llistat apunts + quota)
│   ├── NoteScreen.tsx           # Vista apunt professor (PDF, àudio, resum)
│   ├── ClassroomScreen.tsx      # Gestió d'aula (notes, alumnes)
│   ├── StudentDashboard.tsx     # Dashboard alumne (aules matriculades)
│   ├── StudentClassroomScreen.tsx # Apunts d'una aula (alumne)
│   ├── StudentNoteScreen.tsx    # Vista apunt alumne
│   ├── PricingScreen.tsx        # Plans de subscripció
│   ├── SubscriptionPage.tsx     # Gestió subscripció
│   ├── RaspberryScreen.tsx      # Gestió dispositius Raspberry Pi
│   └── SettingsScreen.tsx       # Configuració (idiomes, canvi contrasenya)
│
├── services/
│   └── api.ts                   # Client Axios amb interceptors JWT + refresh
├── types/
│   └── api.ts                   # Tipus compartits (Note, Classroom, UserProfile, etc.)
├── App.tsx                      # Routing amb ProtectedRoute + React.lazy
└── main.tsx                     # Punt d'entrada
```

### Tecnologies Frontend

- **React 18** amb **TypeScript**
- **Vite 8** com a bundler
- **Tailwind CSS** + **shadcn/ui** (Radix UI Primitives)
- **React Router DOM** per navegació client-side
- **Axios** amb interceptors per JWT i refresh tokens
- **React Markdown** per renderitzar apunts
- **Sonner** per notificacions toast
- **Lucide React** per iconografia
- **jsPDF + html2canvas** (professor) / **react-to-pdf** (alumne) per exportar PDF

### Rutes

| Ruta | Accés | Pàgina |
|------|-------|--------|
| `/` | Públic | LandingScreen |
| `/login` | Públic | LoginScreen |
| `/register` | Públic | RegisterScreen |
| `/forgot-password` | Públic | ForgotPasswordScreen |
| `/pricing` | Públic | PricingScreen |
| `/notes` | Professor | DashboardScreen |
| `/notes/:id` | Professor | NoteScreen |
| `/classrooms/:id` | Professor | ClassroomScreen |
| `/settings` | Professor | SettingsScreen |
| `/raspberry` | Professor | RaspberryScreen |
| `/subscription` | Professor | SubscriptionPage |
| `/student` | Alumne | StudentDashboard |
| `/student/class/:id` | Alumne | StudentClassroomScreen |
| `/student/note/:id` | Alumne | StudentNoteScreen |

---

## Client Raspberry Pi — SmartNotes.Raspberry

Client Python que corre a la Raspberry Pi per gravar àudio i pujar-lo automàticament.

```
SmartNotes.Raspberry/
├── main.py                  # Màquina d'estats: idle → gravació → pujada
├── config.json              # Configuració (API URL, pins, dispositiu àudio)
├── config.example.json      # Exemple de configuració
├── install.sh               # Script d'instal·lació (dependències + autostart)
├── requirements.txt         # Dependències Python
└── sounds/                  # Efectes sonors (beep inici/fi)
```

### Característiques

- **Màquina d'estats**: `idle → recording → stopping → uploading → idle`
- **Gravació** amb `ReSpeaker 2-mics` (estèreo) o micròfon USB
- **Direcció del servo** basada en RMS esquerre/dret per assignar altaveu
- **Servo HD-3001HB**: ±90°, 30° per step, mínim 5s entre moviments
- **Botons GPIO**: 26 (encendre), 27 (gravar)
- **Pujada** automàtica a l'API amb clau global + número de sèrie
- **Detecta idioma** via ffmpeg + mostra de 30s

### Configuració (`config.json`)

```json
{
  "api_url": "https://smartnotes-api-971k.onrender.com/api",
  "api_key": "...",
  "audio_output_device": "plughw:2,0",
  "btn_power_pin": 26,
  "btn_record_pin": 27
}
```

---

## Flux Principal

### 1. **Gravació** (Raspberry Pi)
```
[Botó] → Grava àudio MP3 → Mostra "pujant..." → POST /api/raspberry/upload
```

### 2. **Processament** (Backend Worker)
```
Upload → ffprobe (durada) → Deducció segons → Cua → ffmpeg (neteja) → 
Whisper (transcripció) → Llama (resum) → R2 (àudio millorat) → Nota creada
```

### 3. **Visualització** (Frontend)
```
Dashboard → Polling cada 5s → Notes "processing" detectades per [⏳] →
Quan acaba: contingut markdown + reproductor àudio + PDF exportable
```

### 4. **Aules i Subscripcions**
```
Professor: Crear aula → Codi 6 lletres → Alumne s'hi uneix →
Professor mou apunts a l'aula → Alumne els veu (només lectura)
Subscripció: Free/Pro/Enterprise → Segons/mes per transcriure
```

---

## Models de Dades (PostgreSQL)

| Taula | Descripció |
|-------|-----------|
| `Users` | Usuaris (username, email, password_hash, role, seconds_available, preferred_language, lockout) |
| `RefreshTokens` | Refresh tokens JWT (token, expires, device, ip, revoked) |
| `Notes` | Apunts (title, content, job_id, classroom_id, is_public, public_id) |
| `Classrooms` | Aules (name, code, color, user_id professor) |
| `Enrollments` | Matrícules alumne-aula |
| `Transcriptions` | Registre de transcripcions (clean_text, enhanced_audio_path) |
| `SubscriptionPlans` | Plans de subscripció (name, price, seconds_per_month) |
| `UserSubscriptions` | Subscripcions d'usuari (plan_id, start, end, is_active) |
| `RaspberryDevices` | Dispositius Raspberry (serial_number, user_id, last_seen) |

---

## Seguretat

- **JWT tokens** amb expiry + refresh tokens rotatius
- **Rate limiting** per endpoints d'autenticació
- **Contrasenyes** amb BCrypt (salt + hash)
- **Tokens de reset** amb SHA256 (hash emmagatzemat, raw enviat per email)
- **Deducció atòmica** de segons (SQL `UPDATE ... WHERE available >= cost`)
- **CORS** configurable per entorn
- **HTTPS** obligatori (RequireHttpsMetadata = true)
- **Transaccions** `Serializable` per subscripcions
- **DTOs** amb validació `[Required]`, `[EmailAddress]`, `[StringLength]`

---

## Atribució i Llicència

Aquest projecte ha estat desenvolupat íntegrament per **Pol Martínez i Fernández**.

**Copyright © 2026 Pol Martínez i Fernández. Tots els drets reservats.**

Queda prohibida la reproducció, distribució, comunicació pública, transformació o ús comercial d'aquest software sense l'autorització prèvia i per escrit del seu autor. Aquest codi es proporciona únicament per a ús personal i educatiu del seu autor.

Per a qualsevol consulta sobre llicències o col·laboració, contacteu amb l'autor a través del repositori original.

---

## Desplegament

### Backend (Render)
```bash
# Build
dotnet publish -c Release -o out

# Enviroment variables necessàries:
ConnectionStrings__DefaultConnection  # PostgreSQL
Jwt__Secret                          # Clau secreta JWT
Groq__ApiKey                         # Clau API Groq
CloudflareR2__AccessKey              # Clau accés R2
CloudflareR2__SecretKey              # Clau secreta R2
CloudflareR2__ServiceUrl             # URL servei R2
Raspberry__ApiKey                    # Clau global Raspberry Pi
Email__Password                      # Contrasenya d'aplicació Gmail
AllowedOrigins                       # Orígens CORS permesos
```

### Frontend (Vercel)
```bash
npm run build
# Variables d'entorn:
VITE_API_URL  # URL del backend
```

### Raspberry Pi
```bash
bash install.sh
# Editar config.json amb api_url, api_key
# Connectar botons GPIO 26, 27 + servo + micròfon
python3 main.py
```
