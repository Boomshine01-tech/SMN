# SMART-NEST Vision Dashboard

Dashboard Blazor WebAssembly autonome pour visualiser les indicateurs vision SMART-NEST depuis Supabase.

## Architecture

- **Frontend** : Blazor WebAssembly standalone
- **Framework** : .NET 9
- **Données** : Supabase REST API
- **Images d'alertes** : Supabase Storage privé avec URL signée
- **Hébergement** : Render Static Site
- **Configuration** : variables Render injectées au moment du build

> Le dashboard est un frontend statique : il n'y a pas de serveur ASP.NET Core à maintenir sur Render.

## 1. Préparer Supabase

Le projet suppose que ton schéma Supabase contient au minimum :

- `v_minutes_enriched`
- `vision_alerts`

Les noms et champs attendus par le dashboard sont :

### `v_minutes_enriched`

- `ts`
- `count_mean`
- `huddle_frac`
- `mobility_chick_bw_s`
- `temperature`
- `humidity`
- `age_week`

### `vision_alerts`

- `ts`
- `indicator`
- `direction`
- `z`
- `image_path`
- `camera`
- `event_type`

Le frontend utilise la clé **anon/public** uniquement.

**Ne mets jamais une clé Supabase `service_role` dans ce dépôt ou dans une variable exposée au navigateur.**

Les politiques RLS Supabase doivent protéger les données.

## 2. Tester localement

Installer .NET 9 SDK puis :

```bash
dotnet restore
dotnet run
```

Le dashboard est disponible sur :

```text
/vision
```

ou :

```text
/vision/cam1
```

### Configuration locale

Modifier temporairement `wwwroot/config.json` :

```json
{
  "supabaseUrl": "https://TON-PROJET.supabase.co",
  "supabaseAnonKey": "TA_CLE_ANON"
}
```

Ne committe pas une vraie configuration sensible.

## 3. Déployer sur Render

Render recommande un **Static Site** pour une application frontend composée de fichiers statiques. Blazor WebAssembly standalone se publie justement sous forme de fichiers statiques.

Dans Render :

1. **New → Static Site**
2. Connecter le dépôt GitHub.
3. Branch : `main`
4. Build Command :

```text
bash build.sh
```

5. Publish Directory :

```text
publish/wwwroot
```

6. Ajouter les variables d'environnement :

```text
SUPABASE_URL=https://TON-PROJET.supabase.co
SUPABASE_ANON_KEY=TA_CLE_ANON
```

7. Lancer le déploiement.

`build.sh` génère automatiquement `wwwroot/config.json` pendant le build, puis publie l'application.

## 4. Déployer avec `render.yaml`

Le dépôt contient déjà `render.yaml`.

Après avoir poussé le dépôt GitHub, tu peux créer le service depuis Render/Blueprints et renseigner :

```text
SUPABASE_URL
SUPABASE_ANON_KEY
```

## 5. Authentification Supabase

Le composant accepte un jeton Supabase Auth facultatif depuis :

```text
localStorage["smartnest_access_token"]
```

Si ce jeton existe, il est envoyé comme :

```text
Authorization: Bearer <token>
```

Sinon, le dashboard utilise la clé anon.

Cette solution permet de brancher ton système d'authentification Supabase existant sans mettre de `service_role` dans le frontend.

## 6. Synchronisation avec le pipeline vision

Le dashboard ne calcule pas lui-même les indicateurs YOLO/huddle/flow.

Le pipeline vision doit alimenter Supabase avec les données nécessaires à :

```text
v_minutes_enriched
vision_alerts
```

Le dashboard lit ensuite ces données via l'API REST Supabase.

## 7. GitHub

Créer le dépôt :

```bash
git init
git add .
git commit -m "Initial SMART-NEST vision dashboard"
git branch -M main
git remote add origin https://github.com/TON_COMPTE/smartnest-vision-dashboard.git
git push -u origin main
```

Ensuite, chaque push sur `main` peut déclencher automatiquement un nouveau déploiement Render.

## Dépannage

### `SUPABASE_URL is required`

La variable n'est pas configurée dans Render.

### `Supabase 401`

Vérifier la clé anon et l'authentification/RLS.

### `Supabase 403`

Vérifier les politiques RLS de la vue/table concernée.

### Les données sont vides

Vérifier :

- `camera=cam1`
- les noms des colonnes
- les permissions RLS
- les données présentes dans `v_minutes_enriched`

### Une URL `/vision/cam1` retourne 404

Le dépôt contient déjà des règles `rewrite` Render pour `/vision` et `/vision/*`.
Elles renvoient la requête vers `/index.html` afin que le routeur Blazor puisse traiter la route côté navigateur.

