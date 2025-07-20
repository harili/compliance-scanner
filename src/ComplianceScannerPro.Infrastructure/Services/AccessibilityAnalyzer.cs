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
        if (pagesScanned == 0) return 0;

        var criticalIssues = issues.Count(i => i.Severity == IssueSeverity.Critical);
        var warningIssues = issues.Count(i => i.Severity == IssueSeverity.Warning);
        var infoIssues = issues.Count(i => i.Severity == IssueSeverity.Info);

        // Algorithme de scoring RGAA
        var totalPenalty = (criticalIssues * 10) + (warningIssues * 3) + (infoIssues * 1);
        var maxPossiblePenalty = pagesScanned * 50; // 50 points de pénalité max par page
        
        var score = Math.Max(0, 100 - (int)((double)totalPenalty / maxPossiblePenalty * 100));
        return Math.Min(100, score);
    }

    public async Task<AccessibilityGrade> GetGradeFromScoreAsync(int score)
    {
        return score switch
        {
            >= 90 => AccessibilityGrade.A,
            >= 80 => AccessibilityGrade.B,
            >= 70 => AccessibilityGrade.C,
            >= 60 => AccessibilityGrade.D,
            >= 50 => AccessibilityGrade.E,
            _ => AccessibilityGrade.F
        };
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
                        FixSuggestion = "Ajoutez un attribut alt décrivant le contenu de l'image.",
                        CodeExample = $"<img src=\"{src}\" alt=\"Description de l'image\">"
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
                        FixSuggestion = "Utilisez un texte de lien descriptif du contenu ou de la fonction du lien.",
                        CodeExample = "<a href=\"/contact\">Contacter notre équipe support</a>"
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
                        FixSuggestion = "Associez une étiquette au champ avec l'attribut for ou aria-label.",
                        CodeExample = $"<label for=\"{id}\">Libellé du champ</label>\n<input type=\"text\" id=\"{id}\" name=\"{name}\">"
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
                        FixSuggestion = "Vérifiez que le contraste est d'au moins 4.5:1 pour le texte normal.",
                        CodeExample = "color: #333; background: #fff; /* Contraste 12.6:1 */"
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
                FixSuggestion = "Ajoutez un titre descriptif à la page.",
                CodeExample = "<title>Accueil - Mon Site Web</title>"
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
                FixSuggestion = "Ajoutez l'attribut lang à l'élément html.",
                CodeExample = "<html lang=\"fr\">"
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
                        FixSuggestion = "Respectez la hiérarchie des titres sans sauter de niveau.",
                        CodeExample = $"<h{levels[i-1]}>Titre</h{levels[i-1]}>\n<h{levels[i-1]+1}>Sous-titre</h{levels[i-1]+1}>"
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
                FixSuggestion = "Ajoutez un élément main pour identifier le contenu principal.",
                CodeExample = "<main>Contenu principal de la page</main>"
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
                        FixSuggestion = "Vérifiez si cette image est vraiment décorative ou si elle nécessite une description.",
                        CodeExample = "<img src=\"decoration.png\" alt=\"\" role=\"presentation\">"
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
                        FixSuggestion = "Les listes ne doivent contenir que des éléments li comme enfants directs.",
                        CodeExample = "<ul><li>Item 1</li><li>Item 2</li></ul>"
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