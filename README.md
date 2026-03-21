# Smart-Nest — Guide de déploiement

## Stack
| Couche     | Technologie              |
|------------|--------------------------|
| Client     | Blazor WebAssembly .NET 9 |
| Serveur    | ASP.NET Core .NET 9      |
| Base de données | PostgreSQL (Render) |
| Vidéo      | WebRTC + SignalR + TURN  |
| Auth       | JWT Bearer + BCrypt      |

---

## Démarrage local

```bash
# Prérequis : .NET 9 SDK
cd SmartNest.Server
dotnet run
# → https://localhost:7001
```

---

## Déploiement sur Render

### 1. Pousser sur GitHub

```bash
git init
git add .
git commit -m "Initial commit"
git remote add origin https://github.com/VOTRE_USER/smart-nest.git
git push -u origin main
```

### 2. Créer le service sur Render

1. Allez sur [render.com](https://render.com) → **New** → **Blueprint**
2. Connectez votre dépôt GitHub
3. Render détecte automatiquement le `render.yaml` et crée :
   - Un **Web Service** (l'app)
   - Une **PostgreSQL database** (gratuite)

### 3. Variables d'environnement (dashboard Render)

| Variable | Description |
|---|---|
| `JWT_SECRET` | Généré automatiquement par Render |
| `DATABASE_URL` | Lié automatiquement à la DB Render |
| `WebRTC__TurnHost` | Host TURN (défaut : openrelay.metered.ca) |
| `WebRTC__TurnUsername` | Identifiant TURN |
| `WebRTC__TurnCredential` | Mot de passe TURN |

**Optionnel — SMTP email :**

| Variable | Valeur |
|---|---|
| `Smtp__Host` | `smtp.gmail.com` |
| `Smtp__Port` | `587` |
| `Smtp__Username` | votre email |
| `Smtp__Password` | mot de passe d'application Gmail |
| `Smtp__From` | `noreply@votredomaine.com` |

### 4. TURN server en production

Pour un usage intensif, remplacez openrelay par **Metered.ca** :
1. Créez un compte sur [metered.ca](https://metered.ca)
2. Récupérez vos credentials
3. Mettez à jour `WebRTC__TurnHost`, `WebRTC__TurnUsername`, `WebRTC__TurnCredential`

---

## Envoi de données capteurs (IoT / Node-RED)

```bash
# 1. Obtenir un token
curl -X POST https://VOTRE-APP.onrender.com/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"user","password":"pass","rememberMe":true}'

# 2. Envoyer des données capteurs
curl -X POST https://VOTRE-APP.onrender.com/api/sensor \
  -H "Authorization: Bearer VOTRE_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"temperature":27.5,"humidity":63.0,"dust":48.0}'
```

---

## Structure du projet

```
SmartNest/
├── render.yaml                    ← Config déploiement Render
├── Dockerfile                     ← Image Docker
├── SmartNest.Shared/              ← Modèles & DTOs partagés
├── SmartNest.Server/              ← API ASP.NET Core
│   ├── Controllers/
│   ├── Data/AppDbContext.cs
│   ├── Hubs/WebRtcHub.cs
│   ├── Services/
│   └── Program.cs                 ← PostgreSQL + PORT Render auto
└── SmartNest.Client/              ← SPA Blazor WASM
    ├── Pages/
    │   ├── Index.razor            ← Tableau de bord
    │   ├── Login.razor
    │   ├── Register.razor
    │   ├── Environment.razor
    │   ├── Chicks.razor
    │   ├── Alerts.razor
    │   └── Camera.razor           ← WebRTC live + historique
    ├── Services/
    ├── Shared/MainLayout.razor
    └── wwwroot/
        ├── css/app.css
        ├── js/webrtc.js
        └── sender.html            ← Page émetteur webcam
```
