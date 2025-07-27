using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using ComplianceScannerPro.Core.Entities;
using ComplianceScannerPro.Shared.Enums;
using ComplianceScannerPro.Core.Interfaces;
using System.Text.RegularExpressions;

namespace ComplianceScannerPro.Infrastructure.Services;

public class AccessibilityAnalyzer : IAccessibilityAnalyzer
{
    private readonly ILogger<AccessibilityAnalyzer> _logger;

    public AccessibilityAnalyzer(ILogger<AccessibilityAnalyzer> logger)
    {
        _logger = logger;
    }

    public async Task<List<AccessibilityIssue>> AnalyzePageAsync(string url, string content)
    {
        var issues = new List<AccessibilityIssue>();
        var doc = new HtmlDocument();
        doc.LoadHtml(content);

        // RGAA 1.1 - Images sans alternative textuelle
        issues.AddRange(CheckImagesAltText(doc, url));
        
        // RGAA 6.1 - Liens non explicites
        issues.AddRange(CheckLinkText(doc, url));
        
        // RGAA 11.1 - Champs de formulaire sans étiquette
        issues.AddRange(CheckFormLabels(doc, url));
        
        // RGAA 3.2 - Contraste de couleurs (analyse basique)
        issues.AddRange(CheckColorContrast(doc, url));
        
        // RGAA 8.5 - Titre de page
        issues.AddRange(CheckPageTitle(doc, url));
        
        // RGAA 8.3 - Langue de la page
        issues.AddRange(CheckPageLanguage(doc, url));
        
        // RGAA 9.1 - Structure des titres
        issues.AddRange(CheckHeadingStructure(doc, url));
        
        // RGAA 12.6 - Zones de regroupement
        issues.AddRange(CheckLandmarks(doc, url));
        
        // RGAA 1.2 - Images décoratives
        issues.AddRange(CheckDecorativeImages(doc, url));
        
        // RGAA 9.3 - Listes correctement structurées
        issues.AddRange(CheckListStructure(doc, url));

        _logger.LogInformation("Analyse RGAA terminée pour {Url}: {Count} problèmes détectés", url, issues.Count);
        return issues;
    }

    public async Task<int> CalculateScoreAsync(List<AccessibilityIssue> issues, int pagesScanned)
    {
        if (pagesScanned == 0) return 50; // Score de base pour éviter 0/100 démotivant

        var criticalIssues = issues.Count(i => i.Severity == IssueSeverity.Critical);
        var warningIssues = issues.Count(i => i.Severity == IssueSeverity.Warning);
        var infoIssues = issues.Count(i => i.Severity == IssueSeverity.Info);

        // Nouveau scoring constructif basé sur les bonnes pratiques détectées
        var baseScore = CalculateBaseScore(issues, pagesScanned);
        var penaltyReduction = CalculatePenaltyReduction(criticalIssues, warningIssues, infoIssues);
        
        var finalScore = Math.Max(25, baseScore - penaltyReduction); // Minimum 25 pour éviter l'effet démotivant
        return Math.Min(100, finalScore);
    }

    private int CalculateBaseScore(List<AccessibilityIssue> issues, int pagesScanned)
    {
        // Score de base à 75 points si la structure HTML est détectable
        var structuralScore = 75;
        
        // Bonus pour les bonnes pratiques détectées
        var hasTitle = !issues.Any(i => i.RgaaRule == "RGAA_8_5");
        var hasLanguage = !issues.Any(i => i.RgaaRule == "RGAA_8_3");
        var hasMainContent = !issues.Any(i => i.RgaaRule == "RGAA_12_6");
        
        if (hasTitle) structuralScore += 5;
        if (hasLanguage) structuralScore += 3;
        if (hasMainContent) structuralScore += 2;
        
        return Math.Min(85, structuralScore);
    }

    private int CalculatePenaltyReduction(int critical, int warning, int info)
    {
        // Réduction progressive moins punitive
        var penalty = 0;
        
        // Pénalités critiques réduites de 50%
        penalty += Math.Min(20, critical * 2); // Max 20 points au lieu de criticalIssues * 10
        
        // Pénalités warnings réduites
        penalty += Math.Min(8, warning * 1); // Max 8 points au lieu de warningIssues * 3
        
        // Pénalités info minimales
        penalty += Math.Min(2, info / 2); // Max 2 points pour les infos
        
        return penalty;
    }

    public async Task<AccessibilityGrade> GetGradeFromScoreAsync(int score)
    {
        // Seuils ajustés pour approche constructive et motivante
        return score switch
        {
            >= 85 => AccessibilityGrade.A, // Excellent - Conforme RGAA
            >= 70 => AccessibilityGrade.B, // Très bien - Presque conforme
            >= 55 => AccessibilityGrade.C, // Bien - En progression
            >= 40 => AccessibilityGrade.D, // Correct - Améliorations nécessaires
            >= 25 => AccessibilityGrade.E, // Début - Potentiel détecté
            _ => AccessibilityGrade.F       // Critique - Restructuration nécessaire
        };
    }

    // Nouvelle méthode pour calculer le potentiel d'amélioration
    public async Task<int> CalculatePotentialScoreAsync(List<AccessibilityIssue> issues)
    {
        var criticalIssues = issues.Count(i => i.Severity == IssueSeverity.Critical);
        var warningIssues = issues.Count(i => i.Severity == IssueSeverity.Warning);
        var infoIssues = issues.Count(i => i.Severity == IssueSeverity.Info);

        // Score potentiel si tous les problèmes facilement corrigeables sont résolus
        var imageIssues = issues.Count(i => i.RgaaRule == "RGAA_1_1"); // Images = facile à corriger
        var linkIssues = issues.Count(i => i.RgaaRule == "RGAA_6_1");  // Liens = facile à corriger
        var labelIssues = issues.Count(i => i.RgaaRule == "RGAA_11_1"); // Labels = facile à corriger

        var easilyFixableIssues = imageIssues + linkIssues + labelIssues;
        var potentialGain = Math.Min(35, easilyFixableIssues * 2); // Max 35 points de gain

        return Math.Min(100, 85 + potentialGain); // Potentiel réaliste de 85-100
    }

    private List<AccessibilityIssue> CheckImagesAltText(HtmlDocument doc, string url)
    {
        var issues = new List<AccessibilityIssue>();
        var images = doc.DocumentNode.SelectNodes("//img");
        
        if (images != null)
        {
            foreach (var img in images)
            {
                var alt = img.GetAttributeValue("alt", null);
                var src = img.GetAttributeValue("src", "");
                
                if (alt == null && !IsDecorativeImage(img))
                {
                    issues.Add(new AccessibilityIssue
                    {
                        RgaaRule = "RGAA_1_1",
                        Title = "Image sans alternative textuelle",
                        Description = "Cette image porteuse d'information n'a pas d'attribut alt.",
                        Severity = IssueSeverity.Critical,
                        PageUrl = url,
                        ElementSelector = $"img[src='{src}']",
                        ElementHtml = img.OuterHtml,
                        FixSuggestion = $"RGAA 1.1 : Cette image (src=\"{src}\") porte de l'information et doit avoir un attribut alt décrivant précisément son contenu ou sa fonction. Si c'est un graphique, décrivez les données ; si c'est un bouton, décrivez l'action ; si c'est décorative, ajoutez alt=\"\" et role=\"presentation\".",
                        CodeExample = $"<!-- Si informative -->\n<img src=\"{src}\" alt=\"Graphique montrant une augmentation de 25% des ventes en 2024\">\n<!-- Si décorative -->\n<img src=\"{src}\" alt=\"\" role=\"presentation\">"
                    });
                }
            }
        }
        
        return issues;
    }

    private List<AccessibilityIssue> CheckLinkText(HtmlDocument doc, string url)
    {
        var issues = new List<AccessibilityIssue>();
        var links = doc.DocumentNode.SelectNodes("//a[@href]");
        
        if (links != null)
        {
            foreach (var link in links)
            {
                var text = link.InnerText.Trim();
                var href = link.GetAttributeValue("href", "");
                
                var vagueTexts = new[] { "cliquez ici", "ici", "lire la suite", "plus", "voir plus", "en savoir plus" };
                
                if (string.IsNullOrWhiteSpace(text) || vagueTexts.Any(vt => text.ToLower().Contains(vt)))
                {
                    issues.Add(new AccessibilityIssue
                    {
                        RgaaRule = "RGAA_6_1",
                        Title = "Lien non explicite",
                        Description = "Ce lien n'est pas explicite hors contexte.",
                        Severity = IssueSeverity.Critical,
                        PageUrl = url,
                        ElementSelector = $"a[href='{href}']",
                        ElementHtml = link.OuterHtml,
                        FixSuggestion = $"RGAA 6.1 : Ce lien avec le texte \"{text}\" n'est pas assez explicite. Le texte doit permettre de comprendre la fonction ou la destination du lien hors contexte. Évitez 'cliquez ici', 'en savoir plus', 'lire la suite'. Préférez un texte auto-porteur ou utilisez aria-label pour compléter.",
                        CodeExample = $"<!-- Au lieu de : {text} -->\\n<a href=\\\"{href}\\\">Télécharger le rapport annuel 2024 (PDF, 2MB)</a>\\n<!-- Ou avec aria-label -->\\n<a href=\\\"{href}\\\" aria-label=\\\"En savoir plus sur nos services d'audit RGAA\\\">{text}</a>"
                    });
                }
            }
        }
        
        return issues;
    }

    private List<AccessibilityIssue> CheckFormLabels(HtmlDocument doc, string url)
    {
        var issues = new List<AccessibilityIssue>();
        var inputs = doc.DocumentNode.SelectNodes("//input[@type!='hidden' and @type!='submit' and @type!='button'] | //textarea | //select");
        
        if (inputs != null)
        {
            foreach (var input in inputs)
            {
                var id = input.GetAttributeValue("id", "");
                var name = input.GetAttributeValue("name", "");
                var label = doc.DocumentNode.SelectSingleNode($"//label[@for='{id}']");
                
                if (label == null && string.IsNullOrWhiteSpace(input.GetAttributeValue("aria-label", "")))
                {
                    issues.Add(new AccessibilityIssue
                    {
                        RgaaRule = "RGAA_11_1",
                        Title = "Champ de formulaire sans étiquette",
                        Description = "Ce champ de formulaire n'a pas d'étiquette associée.",
                        Severity = IssueSeverity.Critical,
                        PageUrl = url,
                        ElementSelector = $"input[name='{name}']",
                        ElementHtml = input.OuterHtml,
                        FixSuggestion = $"RGAA 11.1 : Ce champ de formulaire (name=\"{name}\") n'a pas d'étiquette associée. Chaque champ doit avoir un label explicite lié par l'attribut 'for', ou utiliser aria-label/aria-labelledby. Le placeholder n'est pas suffisant car il disparaît lors de la saisie.",
                        CodeExample = $"<!-- Solution 1 : Label classique -->\\n<label for=\\\"{id}\\\">Adresse email (obligatoire)</label>\\n<input type=\\\"email\\\" id=\\\"{id}\\\" name=\\\"{name}\\\" required>\\n\\n<!-- Solution 2 : aria-label -->\\n<input type=\\\"email\\\" name=\\\"{name}\\\" aria-label=\\\"Adresse email obligatoire\\\" required>"
                    });
                }
            }
        }
        
        return issues;
    }

    private List<AccessibilityIssue> CheckColorContrast(HtmlDocument doc, string url)
    {
        var issues = new List<AccessibilityIssue>();
        // Analyse basique - dans la vraie implémentation, on utiliserait un moteur de rendu
        var styles = doc.DocumentNode.SelectNodes("//style");
        if (styles == null) return issues;
        
        foreach (var style in styles)
        {
            var css = style.InnerText;
            if (css.Contains("color:") && css.Contains("background"))
            {
                // Détection de contrastes potentiellement problématiques
                var lowContrastPatterns = new[] { "color:#999", "color:#ccc", "color:#ddd" };
                
                if (lowContrastPatterns.Any(pattern => css.Contains(pattern)))
                {
                    issues.Add(new AccessibilityIssue
                    {
                        RgaaRule = "RGAA_3_2",
                        Title = "Contraste de couleur insuffisant",
                        Description = "Le contraste entre le texte et l'arrière-plan pourrait être insuffisant.",
                        Severity = IssueSeverity.Warning,
                        PageUrl = url,
                        ElementSelector = "Voir le CSS",
                        ElementHtml = css,
                        FixSuggestion = "RGAA 3.2 : Le contraste détecté semble insuffisant. Le RGAA exige un ratio minimum de 4.5:1 pour le texte normal et 3:1 pour le texte de grande taille (18pt+ ou gras 14pt+). Utilisez un outil comme WebAIM Contrast Checker pour vérifier et ajustez les couleurs.",
                        CodeExample = "/* Exemples de contrastes conformes RGAA */\n/* Texte sombre sur fond clair : ratio 12.6:1 */\ncolor: #333333; background: #ffffff;\n/* Texte clair sur fond sombre : ratio 15.3:1 */\ncolor: #ffffff; background: #1f1f1f;\n/* Vérifiez sur : https://webaim.org/resources/contrastchecker/ */"
                    });
                }
            }
        }
        
        return issues;
    }

    private List<AccessibilityIssue> CheckPageTitle(HtmlDocument doc, string url)
    {
        var issues = new List<AccessibilityIssue>();
        var title = doc.DocumentNode.SelectSingleNode("//title");
        
        if (title == null || string.IsNullOrWhiteSpace(title.InnerText))
        {
            issues.Add(new AccessibilityIssue
            {
                RgaaRule = "RGAA_8_5",
                Title = "Titre de page manquant",
                Description = "Cette page n'a pas de titre ou le titre est vide.",
                Severity = IssueSeverity.Critical,
                PageUrl = url,
                ElementSelector = "title",
                ElementHtml = title?.OuterHtml ?? "<title></title>",
                FixSuggestion = "RGAA 8.5 : Cette page n'a pas de titre (balise <title>) ou il est vide. Le titre est essentiel pour l'accessibilité car il est la première information lue par les lecteurs d'écran et identifie la page dans les favoris. Il doit être unique et décrire précisément le contenu de la page.",
                CodeExample = "<!-- Titre spécifique et descriptif -->\\n<title>Résultats du scan RGAA - Site exemple.com - ComplianceScannerPro</title>\\n<!-- Structure recommandée : Contenu principal - Section - Nom du site -->"
            });
        }
        
        return issues;
    }

    private List<AccessibilityIssue> CheckPageLanguage(HtmlDocument doc, string url)
    {
        var issues = new List<AccessibilityIssue>();
        var html = doc.DocumentNode.SelectSingleNode("//html");
        
        if (html == null || string.IsNullOrWhiteSpace(html.GetAttributeValue("lang", "")))
        {
            issues.Add(new AccessibilityIssue
            {
                RgaaRule = "RGAA_8_3",
                Title = "Langue de la page non déclarée",
                Description = "La langue principale de la page n'est pas déclarée.",
                Severity = IssueSeverity.Warning,
                PageUrl = url,
                ElementSelector = "html",
                ElementHtml = html?.OuterHtml ?? "<html>",
                FixSuggestion = "RGAA 8.3 : L'élément <html> n'a pas d'attribut 'lang' définissant la langue principale de la page. Cet attribut est obligatoire pour que les technologies d'assistance (lecteurs d'écran) utilisent la bonne prononciation et les bonnes règles linguistiques.",
                CodeExample = "<!-- Pour une page en français -->\\n<html lang=\\\"fr\\\">\\n<!-- Pour une page en anglais -->\\n<html lang=\\\"en\\\">\\n<!-- Pour du français canadien -->\\n<html lang=\\\"fr-CA\\\">"
            });
        }
        
        return issues;
    }

    private List<AccessibilityIssue> CheckHeadingStructure(HtmlDocument doc, string url)
    {
        var issues = new List<AccessibilityIssue>();
        var headings = doc.DocumentNode.SelectNodes("//h1 | //h2 | //h3 | //h4 | //h5 | //h6");
        
        if (headings != null)
        {
            var levels = headings.Select(h => int.Parse(h.Name.Substring(1))).ToList();
            
            for (int i = 1; i < levels.Count; i++)
            {
                if (levels[i] > levels[i-1] + 1)
                {
                    issues.Add(new AccessibilityIssue
                    {
                        RgaaRule = "RGAA_9_1",
                        Title = "Saut de niveau dans la hiérarchie des titres",
                        Description = $"Saut de h{levels[i-1]} à h{levels[i]} sans niveau intermédiaire.",
                        Severity = IssueSeverity.Warning,
                        PageUrl = url,
                        ElementSelector = $"h{levels[i]}",
                        ElementHtml = headings[i].OuterHtml,
                        FixSuggestion = $"RGAA 9.1 : Hiérarchie de titres incorrecte détectée (saut du niveau h{levels[i-1]} vers h{levels[i]}). Les titres doivent suivre un ordre logique sans sauter de niveau : h1 puis h2, puis h3, etc. Cela permet aux utilisateurs de technologies d'assistance de naviguer efficacement dans la structure du document.",
                        CodeExample = $"<!-- Incorrect : saut de niveau -->\\n<h{levels[i-1]}>Titre principal</h{levels[i-1]}>\\n<h{levels[i]}>Sous-titre</h{levels[i]}> ❌\\n\\n<!-- Correct : progression logique -->\\n<h{levels[i-1]}>Titre principal</h{levels[i-1]}>\\n<h{levels[i-1]+1}>Sous-titre</h{levels[i-1]+1}> ✅"
                    });
                }
            }
        }
        
        return issues;
    }

    private List<AccessibilityIssue> CheckLandmarks(HtmlDocument doc, string url)
    {
        var issues = new List<AccessibilityIssue>();
        var hasMain = doc.DocumentNode.SelectSingleNode("//main") != null;
        var hasNav = doc.DocumentNode.SelectSingleNode("//nav") != null;
        
        if (!hasMain)
        {
            issues.Add(new AccessibilityIssue
            {
                RgaaRule = "RGAA_12_6",
                Title = "Zone de contenu principal manquante",
                Description = "La page n'a pas de zone main identifiée.",
                Severity = IssueSeverity.Warning,
                PageUrl = url,
                ElementSelector = "body",
                ElementHtml = "Pas d'élément main trouvé",
                FixSuggestion = "RGAA 12.6 : Cette page n'a pas d'élément <main> pour identifier la zone de contenu principal. La balise <main> (unique par page) permet aux utilisateurs de technologies d'assistance de se rendre directement au contenu principal en évitant la navigation et les en-têtes répétitifs.",
                CodeExample = "<!-- Structure de page accessible -->\\n<header>\\n  <nav>Menu de navigation</nav>\\n</header>\\n<main>\\n  <h1>Titre principal de la page</h1>\\n  <p>Contenu principal unique de cette page...</p>\\n</main>\\n<footer>Pied de page</footer>"
            });
        }
        
        return issues;
    }

    private List<AccessibilityIssue> CheckDecorativeImages(HtmlDocument doc, string url)
    {
        var issues = new List<AccessibilityIssue>();
        var images = doc.DocumentNode.SelectNodes("//img[@alt='']");
        
        if (images != null)
        {
            foreach (var img in images)
            {
                var src = img.GetAttributeValue("src", "");
                if (!IsDecorativeImage(img) && !string.IsNullOrWhiteSpace(src))
                {
                    issues.Add(new AccessibilityIssue
                    {
                        RgaaRule = "RGAA_1_2",
                        Title = "Image possiblement mal marquée comme décorative",
                        Description = "Cette image avec alt vide pourrait être porteuse d'information.",
                        Severity = IssueSeverity.Info,
                        PageUrl = url,
                        ElementSelector = $"img[src='{src}']",
                        ElementHtml = img.OuterHtml,
                        FixSuggestion = $"RGAA 1.2 : Image potentiellement décorative détectée (src=\"{src}\"). Si cette image est purement décorative (ne porte aucune information), elle doit avoir alt=\"\" ET role=\"presentation\" pour être ignorée par les lecteurs d'écran. Si elle porte de l'information, elle doit avoir un alt descriptif.",
                        CodeExample = $"<!-- Si vraiment décorative -->\\n<img src=\\\"{src}\\\" alt=\\\"\\\" role=\\\"presentation\\\">\\n\\n<!-- Si informative -->\\n<img src=\\\"{src}\\\" alt=\\\"Description précise du contenu informationnel\\\">\\n\\n<!-- Alternative CSS pour décoratif -->\\n<div style=\\\"background-image: url({src}); width: 100px; height: 100px;\\\"></div>"
                    });
                }
            }
        }
        
        return issues;
    }

    private List<AccessibilityIssue> CheckListStructure(HtmlDocument doc, string url)
    {
        var issues = new List<AccessibilityIssue>();
        
        // Vérifier les listes imbriquées incorrectement
        var lists = doc.DocumentNode.SelectNodes("//ul | //ol");
        if (lists != null)
        {
            foreach (var list in lists)
            {
                var children = list.ChildNodes.Where(n => n.NodeType == HtmlNodeType.Element).ToList();
                var nonLiChildren = children.Where(n => n.Name != "li").ToList();
                
                if (nonLiChildren.Any())
                {
                    issues.Add(new AccessibilityIssue
                    {
                        RgaaRule = "RGAA_9_3",
                        Title = "Structure de liste incorrecte",
                        Description = "Cette liste contient des éléments qui ne sont pas des items de liste (li).",
                        Severity = IssueSeverity.Warning,
                        PageUrl = url,
                        ElementSelector = list.Name,
                        ElementHtml = list.OuterHtml,
                        FixSuggestion = "RGAA 9.3 : Structure de liste incorrecte détectée. Les éléments <ul>, <ol> et <dl> ne doivent contenir directement que des éléments <li> (ou <dt>/<dd> pour <dl>). Tout autre contenu doit être placé à l'intérieur des <li>. Cette structure permet aux lecteurs d'écran d'annoncer correctement le nombre d'éléments.",
                        CodeExample = "<!-- Incorrect -->\\n<ul>\\n  <p>Titre de liste</p> ❌\\n  <li>Item 1</li>\\n</ul>\\n\\n<!-- Correct -->\\n<p>Titre de liste</p>\\n<ul>\\n  <li>Item 1</li>\\n  <li>Item 2 avec <strong>texte mis en valeur</strong></li>\\n</ul> ✅"
                    });
                }
            }
        }
        
        return issues;
    }

    private bool IsDecorativeImage(HtmlNode img)
    {
        var src = img.GetAttributeValue("src", "").ToLower();
        var role = img.GetAttributeValue("role", "").ToLower();
        
        return role == "presentation" || 
               src.Contains("decoration") || 
               src.Contains("border") || 
               src.Contains("spacer") ||
               src.Contains("pixel.gif");
    }
}