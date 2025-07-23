class ScanResults {
    constructor() {
        this.scanId = window.scanData?.scanId || '';
        this.currentPage = 1;
        this.pageSize = 20;
        this.filters = {
            severity: '',
            rgaaRule: '',
            search: ''
        };
        this.issuesChart = null;
        this.init();
    }

    init() {
        this.bindEvents();
        this.initChart();
        this.loadIssues();
    }

    bindEvents() {
        // Toggle des filtres
        document.getElementById('toggle-filters')?.addEventListener('click', () => {
            const filtersPanel = document.getElementById('filters-panel');
            const toggleBtn = document.getElementById('toggle-filters');
            
            if (filtersPanel && toggleBtn) {
                if (filtersPanel.classList.contains('show')) {
                    filtersPanel.classList.remove('show');
                    toggleBtn.innerHTML = '<i class="fas fa-filter me-1"></i>Filtres';
                } else {
                    filtersPanel.classList.add('show');
                    toggleBtn.innerHTML = '<i class="fas fa-times me-1"></i>Fermer';
                }
            }
        });

        // Application des filtres
        document.getElementById('apply-filters')?.addEventListener('click', () => {
            this.applyFilters();
        });

        // Reset des filtres
        document.getElementById('reset-filters')?.addEventListener('click', () => {
            this.resetFilters();
        });

        // Recherche en temps réel
        let searchTimeout;
        document.getElementById('search-filter')?.addEventListener('input', (e) => {
            clearTimeout(searchTimeout);
            searchTimeout = setTimeout(() => {
                this.filters.search = e.target.value;
                this.currentPage = 1;
                this.loadIssues();
            }, 500);
        });

        // Export CSV
        document.getElementById('export-csv-btn')?.addEventListener('click', () => {
            this.exportToCSV();
        });

        // Entrée sur les filtres
        document.querySelectorAll('#filters-panel input, #filters-panel select').forEach(element => {
            element.addEventListener('keypress', (e) => {
                if (e.key === 'Enter') {
                    this.applyFilters();
                }
            });
        });
    }

    initChart() {
        const ctx = document.getElementById('issuesChart');
        if (!ctx || !window.scanData) return;

        const data = {
            labels: ['Critiques', 'Avertissements', 'Informations'],
            datasets: [{
                data: [
                    window.scanData.criticalIssues,
                    window.scanData.warningIssues,
                    window.scanData.infoIssues
                ],
                backgroundColor: [
                    '#dc3545',
                    '#ffc107',
                    '#0dcaf0'
                ],
                borderWidth: 2,
                borderColor: '#fff'
            }]
        };

        const config = {
            type: 'doughnut',
            data: data,
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    legend: {
                        position: 'bottom',
                        labels: {
                            padding: 20,
                            usePointStyle: true
                        }
                    },
                    tooltip: {
                        callbacks: {
                            label: function(context) {
                                const label = context.label || '';
                                const value = context.parsed;
                                const total = context.dataset.data.reduce((a, b) => a + b, 0);
                                const percentage = total > 0 ? Math.round((value / total) * 100) : 0;
                                return `${label}: ${value} (${percentage}%)`;
                            }
                        }
                    }
                },
                cutout: '60%'
            }
        };

        this.issuesChart = new Chart(ctx, config);

        // Afficher le total au centre
        this.addCenterText();
    }

    addCenterText() {
        if (!this.issuesChart || !window.scanData) return;

        const originalDraw = Chart.controllers.doughnut.prototype.draw;
        Chart.controllers.doughnut.prototype.draw = function() {
            originalDraw.apply(this, arguments);
            
            const chart = this.chart;
            const ctx = chart.ctx;
            const width = chart.width;
            const height = chart.height;
            
            ctx.restore();
            const fontSize = (height / 114).toFixed(2);
            ctx.font = `bold ${fontSize}em Arial`;
            ctx.textBaseline = 'middle';
            ctx.fillStyle = '#333';
            
            const text = window.scanData.totalIssues.toString();
            const subText = 'problèmes';
            
            const textX = Math.round((width - ctx.measureText(text).width) / 2);
            const textY = height / 2 - 10;
            
            ctx.fillText(text, textX, textY);
            
            ctx.font = `${(fontSize * 0.6)}em Arial`;
            const subTextX = Math.round((width - ctx.measureText(subText).width) / 2);
            ctx.fillText(subText, subTextX, textY + 20);
            
            ctx.save();
        };
    }

    applyFilters() {
        this.filters.severity = document.getElementById('severity-filter')?.value || '';
        this.filters.rgaaRule = document.getElementById('rule-filter')?.value || '';
        this.filters.search = document.getElementById('search-filter')?.value || '';
        
        this.currentPage = 1;
        this.loadIssues();
        
        // Fermer le panneau de filtres sur mobile
        if (window.innerWidth < 768) {
            document.getElementById('toggle-filters')?.click();
        }
    }

    resetFilters() {
        document.getElementById('severity-filter').value = '';
        document.getElementById('rule-filter').value = '';
        document.getElementById('search-filter').value = '';
        
        this.filters = { severity: '', rgaaRule: '', search: '' };
        this.currentPage = 1;
        this.loadIssues();
    }

    async loadIssues(page = 1) {
        if (!this.scanId) return;

        this.currentPage = page;
        this.showLoadingState();

        try {
            const params = new URLSearchParams({
                page: this.currentPage.toString(),
                pageSize: this.pageSize.toString()
            });

            if (this.filters.severity) params.append('severity', this.filters.severity);
            if (this.filters.rgaaRule) params.append('rgaaRule', this.filters.rgaaRule);

            const response = await fetch(`/api/v1/scans/${this.scanId}/issues?${params}`);
            const result = await response.json();

            if (result.success && result.data) {
                this.displayIssues(result.data);
                this.updatePagination(result.data);
            } else {
                this.showError(result.message || 'Erreur lors du chargement des problèmes');
            }
        } catch (error) {
            console.error('Erreur lors du chargement des problèmes:', error);
            this.showError('Erreur de communication avec le serveur');
        }
    }

    displayIssues(paginatedData) {
        const container = document.getElementById('issues-container');
        if (!container) return;

        let filteredIssues = paginatedData.items;

        // Appliquer le filtre de recherche côté client
        if (this.filters.search) {
            const searchTerm = this.filters.search.toLowerCase();
            filteredIssues = filteredIssues.filter(issue => 
                issue.title.toLowerCase().includes(searchTerm) ||
                issue.description.toLowerCase().includes(searchTerm) ||
                issue.rgaaRule.toLowerCase().includes(searchTerm)
            );
        }

        if (filteredIssues.length === 0) {
            container.innerHTML = `
                <div class="text-center py-5">
                    <i class="fas fa-search fa-3x text-muted mb-3"></i>
                    <h5 class="text-muted">Aucun problème trouvé</h5>
                    <p class="text-muted">Essayez de modifier vos critères de recherche.</p>
                </div>
            `;
            return;
        }

        container.innerHTML = filteredIssues.map(issue => this.createIssueHTML(issue)).join('');
        
        // Ajouter les event listeners pour les actions
        this.bindIssueActions();
    }

    createIssueHTML(issue) {
        const severityClass = this.getSeverityClass(issue.severity);
        const severityText = this.getSeverityText(issue.severity);
        
        return `
            <div class="issue-item" data-issue-id="${issue.id}">
                <div class="d-flex justify-content-between align-items-start mb-3">
                    <div class="flex-grow-1">
                        <div class="d-flex align-items-center mb-2">
                            <span class="severity-badge ${severityClass} me-2">${severityText}</span>
                            <span class="badge bg-light text-dark me-2">RGAA ${issue.rgaaRule}</span>
                            <a href="${issue.pageUrl}" target="_blank" class="page-link" title="Voir la page">
                                <i class="fas fa-external-link-alt me-1"></i>
                                ${this.truncateUrl(issue.pageUrl)}
                            </a>
                        </div>
                        <h6 class="issue-title mb-2">${issue.title}</h6>
                        <p class="text-muted mb-2">${issue.description}</p>
                        
                        ${issue.elementSelector ? `
                            <div class="mb-2">
                                <small class="text-muted">Sélecteur:</small>
                                <code class="selector-path">${issue.elementSelector}</code>
                            </div>
                        ` : ''}
                        
                        ${issue.elementHtml ? `
                            <details class="mb-2">
                                <summary class="small text-muted" style="cursor: pointer;">Code HTML concerné</summary>
                                <div class="code-block mt-2">${this.escapeHtml(issue.elementHtml)}</div>
                            </details>
                        ` : ''}
                        
                        ${issue.fixSuggestion ? `
                            <div class="fix-suggestion">
                                <div class="d-flex align-items-center mb-2">
                                    <i class="fas fa-lightbulb text-warning me-2"></i>
                                    <strong>Suggestion de correction:</strong>
                                </div>
                                <p class="mb-2">${issue.fixSuggestion}</p>
                                ${issue.codeExample ? `
                                    <details>
                                        <summary class="small" style="cursor: pointer;">Exemple de code</summary>
                                        <div class="code-block mt-2">${this.escapeHtml(issue.codeExample)}</div>
                                    </details>
                                ` : ''}
                            </div>
                        ` : ''}
                    </div>
                    <div class="ms-3">
                        <div class="dropdown">
                            <button class="btn btn-sm btn-outline-secondary dropdown-toggle" type="button" data-bs-toggle="dropdown">
                                <i class="fas fa-ellipsis-v"></i>
                            </button>
                            <ul class="dropdown-menu">
                                <li>
                                    <a class="dropdown-item copy-selector-btn" href="#" data-selector="${issue.elementSelector}">
                                        <i class="fas fa-copy me-2"></i>Copier le sélecteur
                                    </a>
                                </li>
                                <li>
                                    <a class="dropdown-item" href="${issue.pageUrl}" target="_blank">
                                        <i class="fas fa-external-link-alt me-2"></i>Ouvrir la page
                                    </a>
                                </li>
                                <li><hr class="dropdown-divider"></li>
                                <li>
                                    <a class="dropdown-item text-muted" href="#">
                                        <i class="fas fa-clock me-2"></i>
                                        ${new Date(issue.detectedAt).toLocaleString('fr-FR')}
                                    </a>
                                </li>
                            </ul>
                        </div>
                    </div>
                </div>
            </div>
        `;
    }

    bindIssueActions() {
        // Copier le sélecteur
        document.querySelectorAll('.copy-selector-btn').forEach(btn => {
            btn.addEventListener('click', (e) => {
                e.preventDefault();
                const selector = e.target.closest('.copy-selector-btn').dataset.selector;
                if (selector) {
                    this.copyToClipboard(selector);
                    this.showToast('Sélecteur copié dans le presse-papiers');
                }
            });
        });
    }

    updatePagination(paginatedData) {
        const { totalCount, pageNumber, pageSize } = paginatedData;
        const totalPages = Math.ceil(totalCount / pageSize);
        
        // Info de pagination
        const start = (pageNumber - 1) * pageSize + 1;
        const end = Math.min(pageNumber * pageSize, totalCount);
        
        document.getElementById('pagination-info').innerHTML = 
            `Affichage ${start}-${end} sur ${totalCount} problèmes`;
        
        // Contrôles de pagination
        if (totalPages <= 1) {
            document.getElementById('pagination-controls').innerHTML = '';
            return;
        }
        
        let paginationHTML = '<nav><ul class="pagination pagination-sm mb-0">';
        
        // Bouton précédent
        paginationHTML += `
            <li class="page-item ${pageNumber <= 1 ? 'disabled' : ''}">
                <a class="page-link" href="#" data-page="${pageNumber - 1}">Précédent</a>
            </li>
        `;
        
        // Pages
        const startPage = Math.max(1, pageNumber - 2);
        const endPage = Math.min(totalPages, pageNumber + 2);
        
        if (startPage > 1) {
            paginationHTML += '<li class="page-item"><a class="page-link" href="#" data-page="1">1</a></li>';
            if (startPage > 2) {
                paginationHTML += '<li class="page-item disabled"><span class="page-link">...</span></li>';
            }
        }
        
        for (let i = startPage; i <= endPage; i++) {
            paginationHTML += `
                <li class="page-item ${i === pageNumber ? 'active' : ''}">
                    <a class="page-link" href="#" data-page="${i}">${i}</a>
                </li>
            `;
        }
        
        if (endPage < totalPages) {
            if (endPage < totalPages - 1) {
                paginationHTML += '<li class="page-item disabled"><span class="page-link">...</span></li>';
            }
            paginationHTML += `<li class="page-item"><a class="page-link" href="#" data-page="${totalPages}">${totalPages}</a></li>`;
        }
        
        // Bouton suivant
        paginationHTML += `
            <li class="page-item ${pageNumber >= totalPages ? 'disabled' : ''}">
                <a class="page-link" href="#" data-page="${pageNumber + 1}">Suivant</a>
            </li>
        `;
        
        paginationHTML += '</ul></nav>';
        
        document.getElementById('pagination-controls').innerHTML = paginationHTML;
        
        // Bind events
        document.querySelectorAll('#pagination-controls .page-link').forEach(link => {
            link.addEventListener('click', (e) => {
                e.preventDefault();
                if (!e.target.closest('.page-item').classList.contains('disabled')) {
                    const page = parseInt(e.target.dataset.page);
                    if (page && page !== this.currentPage) {
                        this.loadIssues(page);
                        this.scrollToTop();
                    }
                }
            });
        });
    }

    async exportToCSV() {
        if (!this.scanId) return;

        try {
            const params = new URLSearchParams({
                page: '1',
                pageSize: '10000' // Exporter tous les résultats
            });

            if (this.filters.severity) params.append('severity', this.filters.severity);
            if (this.filters.rgaaRule) params.append('rgaaRule', this.filters.rgaaRule);

            const response = await fetch(`/api/v1/scans/${this.scanId}/issues?${params}`);
            const result = await response.json();

            if (result.success && result.data) {
                this.downloadCSV(result.data.items);
            } else {
                this.showError('Erreur lors de l\'export');
            }
        } catch (error) {
            console.error('Erreur lors de l\'export:', error);
            this.showError('Erreur de communication avec le serveur');
        }
    }

    downloadCSV(issues) {
        const headers = ['Règle RGAA', 'Sévérité', 'Titre', 'Description', 'Page', 'Sélecteur', 'Suggestion'];
        const csvContent = [
            headers.join(','),
            ...issues.map(issue => [
                `"${issue.rgaaRule}"`,
                `"${this.getSeverityText(issue.severity)}"`,
                `"${issue.title.replace(/"/g, '""')}"`,
                `"${issue.description.replace(/"/g, '""')}"`,
                `"${issue.pageUrl}"`,
                `"${issue.elementSelector || ''}"`,
                `"${(issue.fixSuggestion || '').replace(/"/g, '""')}"`
            ].join(','))
        ].join('\n');

        const blob = new Blob([csvContent], { type: 'text/csv;charset=utf-8;' });
        const link = document.createElement('a');
        const url = URL.createObjectURL(blob);
        
        link.setAttribute('href', url);
        link.setAttribute('download', `problemes-rgaa-${this.scanId}.csv`);
        link.style.visibility = 'hidden';
        
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
    }

    // Utilitaires
    getSeverityClass(severity) {
        const classMap = {
            'Critical': 'severity-critical',
            'Warning': 'severity-warning',
            'Info': 'severity-info'
        };
        return classMap[severity] || 'bg-secondary';
    }

    getSeverityText(severity) {
        const textMap = {
            'Critical': 'Critique',
            'Warning': 'Avertissement',
            'Info': 'Information'
        };
        return textMap[severity] || severity;
    }

    truncateUrl(url, maxLength = 50) {
        if (url.length <= maxLength) return url;
        return url.substring(0, maxLength) + '...';
    }

    escapeHtml(unsafe) {
        return unsafe
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;")
            .replace(/'/g, "&#039;");
    }

    async copyToClipboard(text) {
        try {
            await navigator.clipboard.writeText(text);
        } catch (err) {
            // Fallback pour les navigateurs anciens
            const textArea = document.createElement('textarea');
            textArea.value = text;
            textArea.style.position = 'fixed';
            textArea.style.left = '-999999px';
            textArea.style.top = '-999999px';
            document.body.appendChild(textArea);
            textArea.focus();
            textArea.select();
            document.execCommand('copy');
            textArea.remove();
        }
    }

    showLoadingState() {
        document.getElementById('issues-container').innerHTML = `
            <div class="text-center py-5">
                <div class="spinner-border text-primary" role="status">
                    <span class="visually-hidden">Chargement...</span>
                </div>
                <p class="text-muted mt-2">Chargement des problèmes...</p>
            </div>
        `;
    }

    showError(message) {
        document.getElementById('issues-container').innerHTML = `
            <div class="text-center py-5">
                <i class="fas fa-exclamation-triangle fa-3x text-danger mb-3"></i>
                <h5 class="text-danger">Erreur</h5>
                <p class="text-muted">${message}</p>
                <button class="btn btn-primary" onclick="location.reload()">Recharger</button>
            </div>
        `;
    }

    showToast(message) {
        // Créer une notification toast
        const toast = document.createElement('div');
        toast.className = 'toast align-items-center text-white bg-success border-0 position-fixed';
        toast.style.cssText = 'top: 20px; right: 20px; z-index: 9999;';
        toast.setAttribute('role', 'alert');
        toast.setAttribute('aria-live', 'assertive');
        toast.setAttribute('aria-atomic', 'true');
        
        toast.innerHTML = `
            <div class="d-flex">
                <div class="toast-body">
                    <i class="fas fa-check-circle me-2"></i>
                    ${message}
                </div>
                <button type="button" class="btn-close btn-close-white me-2 m-auto" data-bs-dismiss="toast"></button>
            </div>
        `;

        document.body.appendChild(toast);
        const bsToast = new bootstrap.Toast(toast);
        bsToast.show();

        // Supprimer après 3 secondes
        setTimeout(() => {
            if (toast.parentNode) {
                toast.parentNode.removeChild(toast);
            }
        }, 3000);
    }

    scrollToTop() {
        document.getElementById('issues-container').scrollIntoView({ 
            behavior: 'smooth', 
            block: 'start' 
        });
    }
}

// Initialiser les résultats quand le DOM est prêt
document.addEventListener('DOMContentLoaded', () => {
    new ScanResults();
});