using HtmlAgilityPack;
using Markdig;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;

namespace GeradorGithub
{
    class Program
    {
        private static readonly string nomePastaArtigos = @"artigos";
        private static readonly string diretorioDestinoArtigosHtml = @"C:\Users\olive\code\oliveira-michel.github.io\" + nomePastaArtigos;
        private static readonly string enderecoTemplateHtmlArtigo = @"C:\Users\olive\code\oliveira-michel.github.io\artigos\template.htm";
        private static readonly string enderecoIndexHtml = @"C:\Users\olive\code\oliveira-michel.github.io\index.htm";
        private static readonly string enderecoTemplateHtmlCard = @"C:\Users\olive\code\oliveira-michel.github.io\template-card.htm";
        private static readonly string[] enderecosArtigosMarkDown =
            new string[] {
                @"C:\Users\olive\code\artigos\meugithubio\artigo.md",
                @"C:\Users\olive\code\guias-api\design-rest-api\versionamento-insights.md",
                @"C:\Users\olive\code\guias-api\guia-visual\rest-basico.md",
                @"C:\Users\olive\code\guias-api\definindo-contratos-rest-api\guia.md",
                @"C:\Users\olive\code\guias-api\design-rest-api\guia.md"
                };
        internal struct Artigo
        {
            public string EnderecoDestino;
            public string Html;
            public string enderecoImagemOriginal;
            public string enderecoImagemDestino;
        }
        public static bool LerValorComentarioMarkDown(string linhaCorrente, string nomeChave, out string valorComentario)
        {
            int tamanhoChave = nomeChave.Length;
            int posicaoInicioValor = linhaCorrente.IndexOf(nomeChave) + tamanhoChave;

            valorComentario = posicaoInicioValor > tamanhoChave//sinal que veio mais conteúdo depois da chave
                ? linhaCorrente.Substring(posicaoInicioValor, linhaCorrente.LastIndexOf(")") - posicaoInicioValor)
                : string.Empty;

            return posicaoInicioValor > tamanhoChave;
        }
        static void Main(string[] args)
        {
            var templateHtmlArtigo = File.ReadAllText(enderecoTemplateHtmlArtigo);
            var templateHtmlCard = File.ReadAllText(enderecoTemplateHtmlCard);

            var cardFinaisEmHtml = new List<string>();
            var hashtagsMenu = new HashSet<string>();
            var htmlArticles = new List<Artigo>();

            var htmlDocument = new HtmlDocument();

            MarkdownPipeline pipelineToAdvancedExtensions = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();

            foreach (string enderecoArtigoMarkdown in enderecosArtigosMarkDown)
            {
                string[] markdown = File.ReadAllLines(enderecoArtigoMarkdown);

                //Informações que vêm dos comentários nos .md
                DateTime dataCriacao = DateTime.MinValue;
                var resumo = string.Empty;
                var nomeArquivo = string.Empty;
                var linkArquivo = string.Empty;
                var titulo = string.Empty;
                var hashtags = new HashSet<string>();
                var imagem = string.Empty;

                //Percorre os metadados nos comentários dos Markdown e guarda em variável
                var i = 0;
                while (markdown[i].StartsWith("[//]: #") || markdown[i].StartsWith("# ")) //comentário ou primeiro título
                {
                    if (LerValorComentarioMarkDown(markdown[i], "data-criacao:", out string mdDataCriacao))
                    {
                        dataCriacao = DateTime.Parse(mdDataCriacao);
                        goto ContinueLoop;
                    }

                    if (LerValorComentarioMarkDown(markdown[i], "resumo:", out string mdResumo))
                    {
                        resumo = mdResumo;
                        goto ContinueLoop;
                    }

                    if (LerValorComentarioMarkDown(markdown[i], "imagem:", out string mdImagem))
                    {
                        imagem = mdImagem;
                        goto ContinueLoop;
                    }

                    if (LerValorComentarioMarkDown(markdown[i], "hashtags:", out string mdHashtags))
                    {
                        foreach (string hashtag in mdHashtags.Split(" "))
                        {
                            hashtags.Add(hashtag);
                            hashtagsMenu.Add(hashtag);
                        }
                        goto ContinueLoop;
                    }

                    //montagem do nome do arquivo html à partir do título
                    if (markdown[i].StartsWith("# "))
                    {
                        titulo = markdown[i].Substring(2);
                        nomeArquivo = titulo;
                        nomeArquivo = new string(nomeArquivo
                                    .Normalize(NormalizationForm.FormD)
                                    .Where(ch => char.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                                    .ToArray());//remoção de cedilhas, acentuação 
                        nomeArquivo = nomeArquivo.ToLower();
                        Regex.Replace(nomeArquivo, "[^0-9a-z]+", "");//remoção de caracteres especiais
                        nomeArquivo = nomeArquivo.Replace(" ", "-");
                        nomeArquivo = nomeArquivo.Replace(":", "");

                        break;//interrompe busca, pois os metadados sempre vêm antes do título
                    }

                ContinueLoop:
                    i++;
                    continue;
                }//Fim metadados

                //Valida metadados obrigatórios
                if (dataCriacao == DateTime.MinValue) throw new Exception("Sem data de criação nos comentários do markdown");
                if (string.IsNullOrEmpty(resumo)) throw new Exception("Sem resumo nos comentários do markdown");
                if (string.IsNullOrEmpty(nomeArquivo)) throw new Exception("Sem título principal no markdown");
                if (hashtags.Count == 0) throw new Exception("Sem hashtags no markdown");

                //Converte o markdown para HTML
                var artigoHtml = Markdown.ToHtml(string.Join(Environment.NewLine, markdown), pipelineToAdvancedExtensions);
                htmlDocument.LoadHtml(artigoHtml);

                //Percorre os títulos e monta o id que é referenciado pelo índice
                for (int hLevel = 1; hLevel <= 6; hLevel++)
                {
                    for (int hItem = 0; hItem < htmlDocument?.DocumentNode?.SelectNodes($"h{hLevel}")?.Count; hItem++)
                    {
                        var idAnchor = HttpUtility.HtmlEncode(
                            htmlDocument.DocumentNode.SelectNodes($"h{hLevel}")[hItem].LastChild.InnerHtml.ToLower()
                            .Replace(" ", "-")
                            .Replace(">", "")
                            .Replace("&gt;", "")
                            .Replace(",", "")
                        );

                        var anchorId = htmlDocument.CreateElement("a");
                        anchorId.Attributes.Add("id", idAnchor);

                        htmlDocument.DocumentNode.SelectNodes($"h{hLevel}")[hItem].ChildNodes.Insert(0, anchorId);
                    }
                }

                //Define o diretório onde será gravado o artigo
                var diretoriosData = dataCriacao.ToString(@"\\yyyy\\MM\\dd");

                var caminhoImagemDestino = string.Empty;
                var caminhoImagemOriginal = string.Empty;

                //Define o link para a imagem do header do artigo
                if (!string.IsNullOrEmpty(imagem))
                {
                    caminhoImagemDestino = $"{diretorioDestinoArtigosHtml}{diretoriosData}\\{imagem}";

                    var estruturaArquivoArtigo = enderecoArtigoMarkdown.Split("\\");
                    estruturaArquivoArtigo[estruturaArquivoArtigo.Length - 1] = imagem;
                    caminhoImagemOriginal = string.Join("\\", estruturaArquivoArtigo);

                    var divImage = htmlDocument.CreateElement("div");
                    divImage.Attributes.Add("class", "imageHeader");
                    divImage.Attributes.Add("style", $"background-image: url('{imagem}')");

                    htmlDocument.DocumentNode.ChildNodes.Insert(0, divImage);
                }

                //coloca o HTML do artigo em MarkDown no Content
                artigoHtml = templateHtmlArtigo.Replace(@"<div class=""content""></div>", @"<div class=""content"">"
                        + Environment.NewLine
                        + htmlDocument.DocumentNode.OuterHtml
                        + Environment.NewLine
                        + "</div>");

                //Cria o diretório para gravar o artigo
                Directory.CreateDirectory(diretorioDestinoArtigosHtml + diretoriosData);

                //Armazena o artigo pra ser gravado em disco depois
                htmlArticles.Add(new Artigo()
                {
                    EnderecoDestino = $"{diretorioDestinoArtigosHtml}{diretoriosData}\\{nomeArquivo}.htm",
                    Html = artigoHtml,
                    enderecoImagemOriginal = caminhoImagemOriginal,
                    enderecoImagemDestino = caminhoImagemDestino
                });

                //Cria um novo card para ser adicionado no Index depois
                linkArquivo = $"{nomePastaArtigos}/{dataCriacao.ToString(@"yyyy/MM/dd")}/{nomeArquivo}.htm";
                var linkimagem = !string.IsNullOrEmpty(imagem) ? $"{nomePastaArtigos}/{dataCriacao.ToString(@"yyyy/MM/dd")}/{imagem}" : "";

                //Substitui os valores no HTML do card
                var card = templateHtmlCard
                    .Replace("#link-artigo", linkArquivo)
                    .Replace("#link-imagem", linkimagem)
                    .Replace("#titulo", titulo)
                    .Replace("#resumo", resumo)
                    .Replace("#data", dataCriacao.ToString("dd-MM-yyyy"))
                    .Replace("#hashtags", string.Join(" ", hashtags));

                //Armazena para serem adicionados ao index depois
                cardFinaisEmHtml.Add(card);
            }//Fim da carga dos artigos

            //Monta a lista de hashtags com <a> para colocar como menu no index.htm
            var menuHtmlIndex = string.Join(
                    Environment.NewLine,
                    hashtagsMenu.OrderBy(hash => hash).Select(hash => $"<a href='#{hash}' onclick=\"filterHashTag('{hash}')\">#{hash}</a>").ToArray()
                );

            //Monta a lista de hashtags com <a> para colocar como menu nos artivos
            var menuHtmlArtigos = string.Join(
                    Environment.NewLine,
                    hashtagsMenu.OrderBy(hash => hash).Select(hash => $"<a href ='../../../../index.htm#{hash}' onclick=\"filterHashTag('{hash}')\">#{hash}</a>").ToArray()
                );

            //Grava os artigos em disco
            foreach (var artigo in htmlArticles)
            {
                //acrescenta a lista de hashtags no menu
                htmlDocument.LoadHtml(artigo.Html);
                htmlDocument.DocumentNode.SelectSingleNode("//div[@class='links']/div[@class='hashTags']").InnerHtml = menuHtmlArtigos;
                //Grava em disco
                File.WriteAllText(artigo.EnderecoDestino, htmlDocument.DocumentNode.OuterHtml, Encoding.UTF8);
                //Copia para o diretório de destino a imagem do header
                if (!string.IsNullOrEmpty(artigo.enderecoImagemOriginal))
                    File.Copy(artigo.enderecoImagemOriginal, artigo.enderecoImagemDestino, true);
            }

            //Monta a lista de cards no index.htm
            htmlDocument.LoadHtml(File.ReadAllText(enderecoIndexHtml));
            htmlDocument.DocumentNode.SelectSingleNode("//div[@class='grid']").InnerHtml = string.Join(Environment.NewLine, cardFinaisEmHtml);

            //Monta a lista de hashtags no menu
            htmlDocument.DocumentNode.SelectSingleNode("//div[@class='links']/div[@class='hashTags']").InnerHtml = menuHtmlIndex;

            //Grava em disco
            File.WriteAllText(enderecoIndexHtml, htmlDocument.DocumentNode.OuterHtml, Encoding.UTF8);
        }
    }
}
