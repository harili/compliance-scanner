class ScanInterface {
    constructor() {
        this.currentScanId = null;
        this.pollInterval = null;
        this.pollIntervalMs = 2000; // 2 secondes
        this.logMessages = [];
        this.init();
    }

    init() {
        this.bindEvents();
        this.loadRecentScans();
        this.handlePreSelectedWebsite();
    }

    bindEvents() {
        // Gestion des boutons de démarrage de scan
        document.querySelectorAll('.start-scan-btn').forEach(btn => {
            btn.addEventListener('click', (e) => {
                e.preventDefault();
                const websiteId = parseInt(e.target.dataset.websiteId);
                const websiteName = e.target.closest('.website-card').dataset.websiteName;
                const websiteUrl = e.target.closest('.website-card').dataset.websiteUrl;
                this.startScan(websiteId, websiteName, websiteUrl);
            });
        });

        // Bouton d'annulation
        document.getElementById('cancel-scan-btn')?.addEventListener('click', () => {
            this.cancelScan();
        });

        // Toggle des logs
        document.getElementById('toggle-logs')?.addEventListener('click', () => {
            const icon = document.querySelector('#toggle-logs i');
            if (icon) {
                icon.classList.toggle('fa-chevron-down');
                icon.classList.toggle('fa-chevron-up');
            }
        });
    }

    async startScan(websiteId, websiteName, websiteUrl) {
        try {
            const button = document.querySelector(`[data-website-id="${websiteId}"]`);
            this.showLoadingState(button);

            const response = await fetch('/api/v1/scans/start', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify({ websiteId })
            });

            const result = await response.json();

            if (result.success && result.data) {
                this.currentScanId = result.data.scanId;
                this.initiateScanProgress(websiteName, websiteUrl);
                this.startPolling();
                this.addLogMessage(`Scan ${result.data.scanId} démarré pour ${websiteName}`);
            } else {
                const errorMessage = result.errors?.length > 0 ? result.errors[0] : result.message || 'Erreur lors du démarrage du scan';
                this.showError(errorMessage);
                const button = document.querySelector(`[data-website-id="${websiteId}"]`);
                this.hideLoadingState(button);
            }
        } catch (error) {
            console.error('Erreur lors du démarrage du scan:', error);
            this.showError('Erreur de communication avec le serveur');
            const button = document.querySelector(`[data-website-id="${websiteId}"]`);
            this.hideLoadingState(button);
        }
    }

    async cancelScan() {
        if (!this.currentScanId) return;

        try {
            // TODO: Implémenter l'endpoint d'annulation
            this.stopPolling();
            this.addLogMessage(`Scan ${this.currentScanId} annulé par l'utilisateur`);
            this.updateScanStatus('Annulé', 'warning');
            this.showScanSelection();
        } catch (error) {
            console.error('Erreur lors de l\'annulation:', error);
        }
    }

    initiateScanProgress(websiteName, websiteUrl) {
        // Cacher la sélection et afficher la progression
        document.getElementById('scan-selection-card')?.classList.add('d-none');
        document.getElementById('scan-progress-card')?.classList.remove('d-none');
        document.getElementById('scan-results-card')?.classList.add('d-none');

        // Mettre à jour les informations du site
        document.getElementById('scan-website-name').textContent = websiteName;
        document.getElementById('scan-website-url').textContent = websiteUrl;

        // Réinitialiser les métriques
        this.updateProgress(0);
        this.updateMetrics(0, 0, 'En attente');
        this.updateSteps('select', 'completed');
        this.updateSteps('crawl', 'processing');

        // Réinitialiser les logs
        this.logMessages = [];
        this.updateLogDisplay();
    }

    startPolling() {
        if (this.pollInterval) {
            clearInterval(this.pollInterval);
        }

        this.pollInterval = setInterval(() => {
            this.checkScanStatus();
        }, this.pollIntervalMs);
    }

    stopPolling() {
        if (this.pollInterval) {
            clearInterval(this.pollInterval);
            this.pollInterval = null;
        }
    }

    async checkScanStatus() {
        if (!this.currentScanId) return;

        try {
            const response = await fetch(`/api/v1/scans/${this.currentScanId}/status`);
            const result = await response.json();

            if (result.success && result.data) {
                this.updateScanProgress(result.data);
            } else {
                console.error('Erreur lors de la récupération du statut:', result.message);
            }
        } catch (error) {
            console.error('Erreur lors du polling:', error);
            this.addLogMessage(`Erreur de communication: ${error.message}`);
        }
    }

    updateScanProgress(progressData) {
        const { status, pagesScanned, progressPercentage, errorMessage } = progressData;

        this.updateProgress(progressPercentage);
        this.updateMetrics(pagesScanned, progressPercentage, this.getStatusText(status));

        // Mise à jour des étapes
        this.updateStepsBasedOnStatus(status, progressPercentage);

        // Messages de statut
        this.updateStatusMessage(status, pagesScanned);

        // Gestion des logs
        this.addLogMessage(this.getProgressLogMessage(status, pagesScanned, progressPercentage));

        // Vérifier si le scan est terminé
        if (status === 'Completed') {
            this.stopPolling();
            this.loadScanResults();
        } else if (status === 'Failed') {
            this.stopPolling();
            this.showError(errorMessage || 'Le scan a échoué');
        }
    }

    async loadScanResults() {
        if (!this.currentScanId) return;

        try {
            const response = await fetch(`/api/v1/scans/${this.currentScanId}`);
            const result = await response.json();

            if (result.success && result.data) {
                this.showScanResults(result.data);
            }
        } catch (error) {
            console.error('Erreur lors du chargement des résultats:', error);
        }
    }

    showScanResults(scanData) {
        // Cacher la progression et afficher les résultats
        document.getElementById('scan-progress-card')?.classList.add('d-none');
        document.getElementById('scan-results-card')?.classList.remove('d-none');

        // Mettre à jour toutes les étapes comme terminées
        this.updateSteps('crawl', 'completed');
        this.updateSteps('analyze', 'completed');
        this.updateSteps('report', 'completed');

        // Afficher les métriques finales
        document.getElementById('final-score').textContent = scanData.score;
        document.getElementById('final-grade').textContent = this.getGradeText(scanData.grade);
        document.getElementById('total-issues').textContent = scanData.totalIssues;
        document.getElementById('pages-analyzed').textContent = scanData.pagesScanned;

        // Configurer les boutons d'action
        document.getElementById('view-results-btn').href = `/scans/${this.currentScanId}/results`;
        document.getElementById('download-report-btn').href = `/api/v1/scans/${this.currentScanId}/report`;

        // Appliquer les couleurs selon le grade
        this.applyGradeColors(scanData.grade);

        this.addLogMessage(`Scan terminé ! Score: ${scanData.score}/100, Grade: ${this.getGradeText(scanData.grade)}`);
        
        // Recharger les scans récents
        setTimeout(() => {
            this.loadRecentScans();
        }, 1000);
    }

    updateProgress(percentage) {
        const progressBar = document.getElementById('scan-progress-bar');
        if (progressBar) {
            progressBar.style.width = `${percentage}%`;
            progressBar.setAttribute('aria-valuenow', percentage);
        }
    }

    updateMetrics(pagesScanned, progressPercentage, status) {
        document.getElementById('pages-scanned').textContent = pagesScanned;
        document.getElementById('progress-percentage').textContent = `${progressPercentage}%`;
        document.getElementById('scan-status').textContent = status;
    }

    updateSteps(stepId, state) {
        const step = document.getElementById(`step-${stepId}`);
        if (step) {
            step.className = `step-indicator ${state}`;
        }
    }

    updateStepsBasedOnStatus(status, progress) {
        switch (status) {
            case 'Pending':
                this.updateSteps('select', 'completed');
                this.updateSteps('crawl', 'processing');
                break;
            case 'Running':
                this.updateSteps('select', 'completed');
                
                if (progress < 40) {
                    // Phase crawling (5-40%)
                    this.updateSteps('crawl', 'processing');
                } else if (progress < 90) {
                    // Phase analyse (40-90%)
                    this.updateSteps('crawl', 'completed');
                    this.updateSteps('analyze', 'processing');
                } else {
                    // Phase finalisation (90-95%)
                    this.updateSteps('crawl', 'completed');
                    this.updateSteps('analyze', 'completed');
                    this.updateSteps('report', 'processing');
                }
                break;
            case 'Completed':
                this.updateSteps('select', 'completed');
                this.updateSteps('crawl', 'completed');
                this.updateSteps('analyze', 'completed');
                this.updateSteps('report', 'completed');
                break;
            case 'Failed':
                // Garder l'état actuel mais indiquer l'échec
                break;
        }
    }

    updateStatusMessage(status, pagesScanned) {
        const messageElement = document.getElementById('status-text');
        const alertElement = document.getElementById('scan-status-message');
        
        if (!messageElement || !alertElement) return;

        let message, alertClass;

        switch (status) {
            case 'Pending':
                message = 'Initialisation du scan...';
                alertClass = 'alert-info';
                break;
            case 'Running':
                // Message plus précis selon les pages scannées
                if (pagesScanned === 0) {
                    message = '🕷️ Exploration et indexation des pages du site...';
                } else if (pagesScanned < 10) {
                    message = `🔍 Analyse RGAA en cours (${pagesScanned} pages traitées)...`;
                } else {
                    message = `🔍 Analyse RGAA avancée (${pagesScanned} pages traitées)...`;
                }
                alertClass = 'alert-primary';
                break;
            case 'Completed':
                message = '✅ Scan terminé avec succès ! Génération du rapport...';
                alertClass = 'alert-success';
                break;
            case 'Failed':
                message = '❌ Le scan a rencontré une erreur.';
                alertClass = 'alert-danger';
                break;
            default:
                message = '❓ Statut inconnu';
                alertClass = 'alert-secondary';
        }

        messageElement.textContent = message;
        alertElement.className = `alert ${alertClass}`;
    }

    addLogMessage(message) {
        const timestamp = new Date().toLocaleTimeString();
        this.logMessages.push(`[${timestamp}] ${message}`);
        this.updateLogDisplay();
    }

    updateLogDisplay() {
        const logContent = document.getElementById('scan-log-content');
        if (logContent) {
            logContent.textContent = this.logMessages.join('\n');
            logContent.scrollTop = logContent.scrollHeight;
        }
    }

    async loadRecentScans() {
        try {
            const response = await fetch('/api/v1/scans?pageSize=5');
            const result = await response.json();

            if (result.success && result.data) {
                this.displayRecentScans(result.data.items);
            }
        } catch (error) {
            console.error('Erreur lors du chargement des scans récents:', error);
        }
    }

    displayRecentScans(scans) {
        const container = document.getElementById('recent-scans-list');
        if (!container) return;

        if (!scans || scans.length === 0) {
            container.innerHTML = `
                <div class="text-center text-muted py-3">
                    <i class="fas fa-inbox fa-2x mb-2"></i>
                    <p class="mb-0">Aucun scan récent</p>
                </div>
            `;
            return;
        }

        container.innerHTML = scans.map(scan => `
            <div class="d-flex justify-content-between align-items-center py-2 border-bottom">
                <div class="flex-grow-1">
                    <div class="fw-semibold">${scan.websiteName}</div>
                    <div class="small text-muted">
                        ${new Date(scan.startedAt).toLocaleDateString('fr-FR')}
                        ${scan.completedAt ? '• Score: ' + scan.score + '/100' : '• En cours'}
                    </div>
                </div>
                <div class="ms-2">
                    ${this.getScanStatusBadge(scan.status, scan.grade)}
                </div>
            </div>
        `).join('');
    }

    getScanStatusBadge(status, grade) {
        switch (status) {
            case 'Completed':
                return `<span class="badge ${this.getGradeBadgeClass(grade)}">${this.getGradeText(grade)}</span>`;
            case 'Running':
                return '<span class="badge bg-primary">En cours</span>';
            case 'Failed':
                return '<span class="badge bg-danger">Échec</span>';
            case 'Pending':
                return '<span class="badge bg-secondary">En attente</span>';
            default:
                return '<span class="badge bg-light text-dark">-</span>';
        }
    }

    getStatusText(status) {
        const statusMap = {
            'Pending': 'En attente',
            'Running': 'En cours',
            'Completed': 'Terminé',
            'Failed': 'Échec',
            'Cancelled': 'Annulé'
        };
        return statusMap[status] || status;
    }

    getGradeText(grade) {
        const gradeMap = {
            'A': 'A - Excellent',
            'B': 'B - Bon',
            'C': 'C - Moyen',
            'D': 'D - Médiocre',
            'F': 'F - Échec'
        };
        return gradeMap[grade] || grade;
    }

    getGradeBadgeClass(grade) {
        const classMap = {
            'A': 'bg-success',
            'B': 'bg-primary',
            'C': 'bg-warning',
            'D': 'bg-orange',
            'F': 'bg-danger'
        };
        return classMap[grade] || 'bg-secondary';
    }

    applyGradeColors(grade) {
        const gradeElement = document.getElementById('final-grade');
        if (gradeElement) {
            gradeElement.className = `metric-badge ${this.getGradeBadgeClass(grade)} text-white`;
        }
    }

    getProgressLogMessage(status, pagesScanned, progress) {
        switch (status) {
            case 'Pending':
                return 'Scan en attente de démarrage...';
            case 'Running':
                return `Progression: ${progress}% - ${pagesScanned} pages analysées`;
            case 'Completed':
                return 'Scan terminé avec succès';
            case 'Failed':
                return 'Scan échoué';
            default:
                return `Statut: ${status}`;
        }
    }

    showError(message) {
        // Créer une notification d'erreur
        const alert = document.createElement('div');
        alert.className = 'alert alert-danger alert-dismissible fade show position-fixed';
        alert.style.cssText = 'top: 20px; right: 20px; z-index: 9999; min-width: 300px;';
        alert.innerHTML = `
            <i class="fas fa-exclamation-triangle me-2"></i>
            ${message}
            <button type="button" class="btn-close" data-bs-dismiss="alert"></button>
        `;

        document.body.appendChild(alert);

        // Supprimer automatiquement après 5 secondes
        setTimeout(() => {
            if (alert.parentNode) {
                alert.parentNode.removeChild(alert);
            }
        }, 5000);
    }

    showLoadingState(specificButton = null) {
        if (specificButton) {
            // Afficher le loading seulement sur le bouton spécifique
            specificButton.disabled = true;
            specificButton.innerHTML = '<i class="fas fa-spinner fa-spin me-1"></i>Démarrage...';
        } else {
            // Fallback : tous les boutons (compatibilité)
            document.querySelectorAll('.start-scan-btn').forEach(btn => {
                btn.disabled = true;
                btn.innerHTML = '<i class="fas fa-spinner fa-spin me-1"></i>Démarrage...';
            });
        }
    }

    hideLoadingState(specificButton = null) {
        if (specificButton) {
            // Restaurer seulement le bouton spécifique
            specificButton.disabled = false;
            specificButton.innerHTML = '<i class="fas fa-play me-1"></i>Scanner';
        } else {
            // Fallback : tous les boutons (compatibilité)
            document.querySelectorAll('.start-scan-btn').forEach(btn => {
                btn.disabled = false;
                btn.innerHTML = '<i class="fas fa-play me-1"></i>Scanner';
            });
        }
    }

    showScanSelection() {
        document.getElementById('scan-selection-card')?.classList.remove('d-none');
        document.getElementById('scan-progress-card')?.classList.add('d-none');
        document.getElementById('scan-results-card')?.classList.add('d-none');
        this.hideLoadingState(); // Ici on restaure tous les boutons car on revient à la sélection
    }
    
    handlePreSelectedWebsite() {
        // Gérer la pré-sélection d'un site web via le paramètre websiteId
        if (window.preSelectedWebsite) {
            const websiteId = window.preSelectedWebsite.Id;
            const websiteName = window.preSelectedWebsite.Name;
            const websiteUrl = window.preSelectedWebsite.Url;
            
            // Trouver la carte correspondante et la mettre en évidence
            const websiteCard = document.querySelector(`[data-website-id="${websiteId}"]`);
            if (websiteCard) {
                // Ajouter une classe visuelle pour indiquer la pré-sélection
                websiteCard.classList.add('pre-selected');
                
                // Faire défiler vers la carte si nécessaire
                websiteCard.scrollIntoView({ behavior: 'smooth', block: 'center' });
                
                // Ajouter du style CSS inline pour un effet visuel immédiat
                const card = websiteCard.querySelector('.card');
                if (card) {
                    card.style.borderColor = '#0d6efd';
                    card.style.borderWidth = '2px';
                    card.style.boxShadow = '0 4px 12px rgba(13, 110, 253, 0.2)';
                }
                
                // Optionnel : démarrer automatiquement le scan après un délai
                // Uncomment si vous voulez un démarrage automatique
                /*
                setTimeout(() => {
                    const startButton = websiteCard.querySelector('.start-scan-btn');
                    if (startButton && !startButton.disabled) {
                        console.log(`Démarrage automatique du scan pour ${websiteName}`);
                        this.startScan(websiteId, websiteName, websiteUrl);
                    }
                }, 2000); // 2 secondes de délai pour que l'utilisateur voie la sélection
                */
            }
        }
    }
}

// Initialiser l'interface quand le DOM est prêt
document.addEventListener('DOMContentLoaded', () => {
    new ScanInterface();
});