# 🔧 Guide de Debug - ComplianceScannerPro

## 🚀 Démarrage rapide

### 1. Lance l'application avec logs détaillés

```bash
./start-with-debug.sh
```

Cette commande :
- ✅ Nettoie et reconstruit le projet
- ✅ Lance l'application avec logs colorisés
- ✅ Affiche les logs de scan avec emojis 🚀✅❌
- ✅ URLs d'accès disponibles

### 2. URLs importantes

- **Application principale** : https://localhost:7293
- **Interface de debug** : `file://$(pwd)/debug-scan-execution.html`
- **Outils admin** : `file://$(pwd)/make-admin.html`

## 🛠️ Outils de debug disponibles

### Interface de debug complète (`debug-scan-execution.html`)

**Fonctionnalités :**

1. **Test de connexion API** - Vérifie si l'API répond
2. **Lancement de scan de test** - Démarre un scan et récupère l'ID  
3. **Monitoring temps réel** - Suit l'évolution du scan toutes les 5s
4. **Test des services individuels** :
   - WebCrawler : teste le crawling d'URLs
   - AccessibilityAnalyzer : teste l'analyse d'accessibilité
   - Pipeline complet : teste tout le processus
5. **Analyse approfondie** - Détails complets d'un scan par ID

### Endpoints API de debug

- `POST /api/v1/debug/test-crawler` - Test WebCrawler
- `POST /api/v1/debug/test-analyzer` - Test AccessibilityAnalyzer  
- `POST /api/v1/debug/test-full-pipeline` - Test pipeline complet
- `GET /api/v1/debug/scan-details/{scanId}` - Détails scan

### Outils admin (`make-admin.html`)

- Donner privilèges admin à `akhy.kays@gmail.com`
- Vérifier le statut admin d'un utilisateur

## 🔍 Processus de diagnostic

### Problème : "Scans bloqués en pending/running"

**Étape 1 : Vérifier la connectivité**
```
Interface debug → Test de connexion API
```

**Étape 2 : Tester les services individuellement**
```
Interface debug → Test WebCrawler avec https://example.com
Interface debug → Test AccessibilityAnalyzer avec https://example.com
Interface debug → Test Pipeline complet
```

**Étape 3 : Lancer un scan de test**
```
Interface debug → Démarrer scan de test (Website ID: 1)
```

**Étape 4 : Monitoring en temps réel**
```
Interface debug → Démarrer le monitoring
Regarder les logs dans la console et l'interface
```

**Étape 5 : Analyse approfondie**
```
Interface debug → Analyser Scan (entrer le Scan ID)
```

## 📊 Logs à surveiller

### Logs de scan (avec emojis)

- `🚀 [SCAN-START]` - Début d'exécution
- `✅ [SCAN-DB]` - Récupération en base
- `🌐 [SCAN-WEBSITE]` - Website trouvé
- `🕷️ [SCAN-PHASE-1]` - Début crawling
- `✅ [SCAN-CRAWL]` - Crawling terminé
- `🔍 [SCAN-PHASE-2]` - Début analyse accessibilité
- `📈 [SCAN-PROGRESS]` - Progression
- `✅ [SCAN-ANALYSIS-COMPLETE]` - Analyse terminée
- `❌ [SCAN-ERROR]` - Erreur critique

## 🗃️ Requêtes SQL utiles

```sql
-- État des scans récents
SELECT "Id", "ScanId", "Status", "StartedAt", "CompletedAt", "PagesScanned", "ErrorMessage" 
FROM "ScanResults" 
ORDER BY "StartedAt" DESC 
LIMIT 10;

-- Scans en cours ou en attente
SELECT "Id", "ScanId", "Status", "StartedAt", "PagesScanned", "ErrorMessage"
FROM "ScanResults" 
WHERE "Status" IN (0, 1) -- Pending=0, Running=1
ORDER BY "StartedAt" DESC;
```

## 🎯 Points de contrôle critiques

### 1. ScanService.ExecuteScanAsync
- ✅ Logs détaillés avec emojis implémentés
- ✅ CancellationToken avec timeout 10min
- ✅ Gestion d'erreurs complète

### 2. WebCrawlerService.CrawlAsync  
- ✅ Timeout 30s par requête HTTP
- ✅ Limite sécurité 100 URLs max
- ✅ Test d'accessibilité préalable

### 3. AccessibilityAnalyzer.AnalyzePageAsync
- ✅ Analyse HTML avec HtmlAgilityPack
- ✅ Détection RGAA automatique

## 🔧 Actions de résolution

### Si le WebCrawler bloque :
- Vérifier la connectivité internet
- Tester avec une URL simple (https://example.com)
- Vérifier les timeouts HTTP (30s)

### Si l'AccessibilityAnalyzer bloque :
- Vérifier que le contenu HTML est récupéré
- Tester avec du HTML simple
- Vérifier les regex RGAA

### Si ExecuteScanAsync ne se lance pas :
- Vérifier Task.Run avec CancellationToken
- Vérifier les logs 🚀 [SCAN-START]
- Contrôler la récupération des entités en base

## 🆘 En cas d'urgence

1. **Arrêter tous les scans :**
```bash
pkill -f "dotnet.*ComplianceScannerPro"
```

2. **Nettoyer la base :**
```sql
UPDATE "ScanResults" SET "Status" = 3 WHERE "Status" IN (0, 1); -- Failed=3
```

3. **Relancer l'application :**
```bash
./start-with-debug.sh
```

## 📞 Utilisation

1. Lance `./start-with-debug.sh`
2. Ouvre `debug-scan-execution.html` dans un navigateur
3. Suis les étapes de diagnostic
4. Analyse les logs colorisés dans le terminal
5. Utilise les outils admin si nécessaire

**C'est parti pour le debug ! 🚀**