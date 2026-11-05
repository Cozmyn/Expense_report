using System.Security.Cryptography;
using System.Text.RegularExpressions;
namespace Decont;
public static class DocumentPages {
 static async Task<(IOriginalDocument File,byte[] Bytes)> Source(Guid id,Guid document,Reports svc,IStore store,HttpContext c){
  var report=await svc.Get(id,c.User);DocumentPreviewTokens.Validate(c,id,document);IOriginalDocument? file=report.Attachments.FirstOrDefault(a=>a.Id==document)??(IOriginalDocument?)report.Lines.SelectMany(l=>l.Evidence).FirstOrDefault(e=>e.Id==document);
  Guard.Require(file!=null,"missing","Document inexistent.","Document not found.",404);Guard.Require(file!.Scan=="Clean","quarantine","Așteaptă scanarea documentului.","Wait for document scanning.");
  var bytes=await store.Get(file.Key,c.RequestAborted);Guard.Require(Convert.ToHexString(SHA256.HashData(bytes))==file.Hash,"integrity","Documentul original a fost modificat.","Original document integrity check failed.");
  c.Response.Headers.CacheControl="private, no-store";c.Response.Headers["X-Content-Type-Options"]="nosniff";return(file,bytes);
 }
 static async Task<int> PdfPages(byte[] bytes,IConfiguration config,CancellationToken ct){
  var directory=Path.Combine(Path.GetTempPath(),"decont-pages-"+Guid.NewGuid());Directory.CreateDirectory(directory);
  try{var input=Path.Combine(directory,"original.pdf");await File.WriteAllBytesAsync(input,bytes,ct);var text=await LocalDocumentExtractor.Run(config["Ocr:PdfInfo"]??"pdfinfo",[input],ct);var match=Regex.Match(text,@"(?m)^Pages:\s*(\d+)");Guard.Require(match.Success&&int.TryParse(match.Groups[1].Value,out var count)&&count>0&&count<=(config.GetValue<int?>("Ocr:MaxPages")??50),"pages","Documentul nu poate fi previzualizat în limita de pagini.","Document exceeds preview page limits or cannot be read.");return int.Parse(match.Groups[1].Value);}finally{Directory.Delete(directory,true);}
 }
 public static async Task<int> Count(string mime,byte[] bytes,IConfiguration config,CancellationToken ct)=>mime=="application/pdf"?await PdfPages(bytes,config,ct):1;
 public static void Map(RouteGroupBuilder api){
  api.MapGet("/reports/{id:guid}/documents/{document:guid}/preview-info",async(Guid id,Guid document,Reports svc,IStore store,Db db,IConfiguration config,HttpContext c)=>{
   var source=await Source(id,document,svc,store,c);var review=await db.DocumentReviews.FindAsync(document);var pages=source.File.Mime=="application/pdf"?(review?.PageCount??await PdfPages(source.Bytes,config,c.RequestAborted)):1;
   return Results.Ok(new{pages,source.File.Name,source.File.Mime,viewer=DocumentPreviewTokens.Issue(c,id,document),expiresInSeconds=300});
  }).RequireRateLimiting("document-preview");
  api.MapGet("/reports/{id:guid}/documents/{document:guid}/pages/{page:int}/preview",async(Guid id,Guid document,int page,Reports svc,IStore store,Db db,IConfiguration config,HttpContext c)=>{
   var source=await Source(id,document,svc,store,c);var file=source.File;var review=await db.DocumentReviews.FindAsync(document);
   var pages=file.Mime=="application/pdf"?(review?.PageCount??await PdfPages(source.Bytes,config,c.RequestAborted)):1;Guard.Require(page>=1&&page<=pages,"page","Pagină invalidă.","Invalid page.",404);
   if(file.Mime is "image/png" or "image/jpeg")return Results.File(source.Bytes,file.Mime);
   var directory=Path.Combine(Path.GetTempPath(),"decont-render-"+Guid.NewGuid());Directory.CreateDirectory(directory);
   try{var input=Path.Combine(directory,file.Mime=="application/pdf"?"source.pdf":"source.heic");var output=Path.Combine(directory,"page");await File.WriteAllBytesAsync(input,source.Bytes,c.RequestAborted);
    if(file.Mime=="application/pdf")await LocalDocumentExtractor.Run(config["Ocr:PdfRenderer"]??"pdftoppm",["-f",page.ToString(),"-l",page.ToString(),"-singlefile","-scale-to","1600","-png",input,output],c.RequestAborted);
    else await LocalDocumentExtractor.Run(config["Ocr:HeicConverter"]??"heif-convert",[input,output+".png"],c.RequestAborted);
    return Results.File(await File.ReadAllBytesAsync(output+".png",c.RequestAborted),"image/png");
   }finally{Directory.Delete(directory,true);}
  }).RequireRateLimiting("document-preview");
 }
}

