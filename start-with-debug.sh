#!/bin/bash

echo "🚀 Démarrage de ComplianceScannerPro avec debug actif..."

# Couleurs pour les logs
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
MAGENTA='\033[0;35m'
CYAN='\033[0;36m'
NC='\033[0m' # No Color

# Fonction pour arrêter proprement
cleanup() {
    echo -e "\n${YELLOW}🛑 Arrêt de l'application...${NC}"
    pkill -f "dotnet.*ComplianceScannerPro"
    exit 0
}

# Capturer Ctrl+C
trap cleanup SIGINT

# Aller dans le dossier Web
cd src/ComplianceScannerPro.Web

echo -e "${GREEN}📍 Répertoire de travail: $(pwd)${NC}"

# Vérifier si l'application est déjà en cours
if pgrep -f "dotnet.*ComplianceScannerPro" > /dev/null; then
    echo -e "${YELLOW}⚠️  Application déjà en cours d'exécution. Arrêt...${NC}"
    pkill -f "dotnet.*ComplianceScannerPro"
    sleep 2
fi

echo -e "${BLUE}🔨 Construction du projet...${NC}"
dotnet build --no-restore

if [ $? -ne 0 ]; then
    echo -e "${RED}❌ Erreur de build${NC}"
    exit 1
fi

echo -e "${GREEN}✅ Build réussi${NC}"

# Variables d'environnement pour plus de logs
export ASPNETCORE_ENVIRONMENT=Development
export Logging__LogLevel__Default=Information
export Logging__LogLevel__ComplianceScannerPro=Debug
export Logging__LogLevel__Microsoft=Warning

echo -e "${MAGENTA}🌐 Démarrage du serveur web...${NC}"
echo -e "${CYAN}📊 URLs disponibles:${NC}"
echo -e "   • Application: https://localhost:7293 (principal)"
echo -e "   • Application: http://localhost:5000 (alternatif)"
echo -e "   • Debug Interface: file://$(pwd)/../../debug-scan-execution.html"
echo -e "   • Admin Tools: file://$(pwd)/../../make-admin.html"
echo -e ""
echo -e "${YELLOW}💡 Conseils de debug:${NC}"
echo -e "   • Les logs détaillés avec emojis 🚀✅❌ apparaîtront ci-dessous"
echo -e "   • Utilisez l'interface de debug pour tester les scans"
echo -e "   • Ctrl+C pour arrêter proprement"
echo -e ""
echo -e "${BLUE}═══════════════════ LOGS APPLICATION ═══════════════════${NC}"

# Démarrer l'application avec logs colorisés
dotnet run --urls="https://localhost:7293;http://localhost:5000" 2>&1 | while IFS= read -r line; do
    # Coloriser les logs selon le contenu
    if [[ $line == *"🚀"* ]]; then
        echo -e "${GREEN}$line${NC}"
    elif [[ $line == *"✅"* ]]; then
        echo -e "${CYAN}$line${NC}"
    elif [[ $line == *"❌"* ]] || [[ $line == *"ERROR"* ]]; then
        echo -e "${RED}$line${NC}"
    elif [[ $line == *"⚠️"* ]] || [[ $line == *"WARNING"* ]]; then
        echo -e "${YELLOW}$line${NC}"
    elif [[ $line == *"SCAN"* ]]; then
        echo -e "${MAGENTA}$line${NC}"
    else
        echo "$line"
    fi
done