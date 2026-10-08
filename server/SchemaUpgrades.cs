using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;
namespace Decont;
public static class SchemaUpgrades {
 // Additive upgrade for pre-receipt-workflow installations; no existing data is rewritten.
 public static async Task Apply(Db db){
  var ddl=db.Database.GenerateCreateScript();
  var table=Regex.Match(ddl,@"CREATE TABLE ""ReportAttachments"" \(.*?\n\);",RegexOptions.Singleline);
  if(!table.Success)throw new InvalidOperationException("Report attachment schema missing");
  await using var tx=await db.Database.BeginTransactionAsync();
  await db.Database.ExecuteSqlRawAsync(table.Value.Replace("CREATE TABLE ","CREATE TABLE IF NOT EXISTS "));
  foreach(System.Text.RegularExpressions.Match index in Regex.Matches(ddl,@"CREATE INDEX ""[^""]+"" ON ""ReportAttachments"".*?;"))await db.Database.ExecuteSqlRawAsync(index.Value.Replace("CREATE INDEX ","CREATE INDEX IF NOT EXISTS "));
  await tx.CommitAsync();
 }
}
