using Microsoft.EntityFrameworkCore;using Microsoft.EntityFrameworkCore.Storage;
using System.Text.RegularExpressions;
namespace Decont;
public static class SchemaUpgrades {
 public static async Task Apply(Db db){
  var ddl=db.Database.GenerateCreateScript();await using var tx=await db.Database.BeginTransactionAsync();
  foreach(System.Text.RegularExpressions.Match table in Regex.Matches(ddl,@"CREATE TABLE ""(?:ReportAttachments|Preferences|Comments|Assignments|SchemaReleases)"" \(.*?\n\);",RegexOptions.Singleline))await db.Database.ExecuteSqlRawAsync(table.Value.Replace("CREATE TABLE ","CREATE TABLE IF NOT EXISTS "));
  foreach(var column in new[]{("Lines","VatOverrideReason","TEXT NULL"),("Decisions","RevisionHash","TEXT NOT NULL DEFAULT ''")}){
   await using var command=db.Database.GetDbConnection().CreateCommand();command.Transaction=tx.GetDbTransaction();command.CommandText=db.Database.IsNpgsql()?"SELECT column_name FROM information_schema.columns WHERE table_schema='public' AND table_name='"+column.Item1+"'":"PRAGMA table_info(\""+column.Item1+"\")";var names=new List<string>();await using(var reader=await command.ExecuteReaderAsync()){while(await reader.ReadAsync())names.Add(reader.GetString(db.Database.IsNpgsql()?0:1));}if(!names.Contains(column.Item2))await db.Database.ExecuteSqlRawAsync("ALTER TABLE \""+column.Item1+"\" ADD COLUMN \""+column.Item2+"\" "+column.Item3);
  }
  foreach(System.Text.RegularExpressions.Match index in Regex.Matches(ddl,@"CREATE (?:UNIQUE )?INDEX ""[^""]+"" ON ""(?:ReportAttachments|Preferences|Comments|Assignments|SchemaReleases)"".*?;"))await db.Database.ExecuteSqlRawAsync(index.Value.Replace("CREATE INDEX ","CREATE INDEX IF NOT EXISTS ").Replace("CREATE UNIQUE INDEX ","CREATE UNIQUE INDEX IF NOT EXISTS "));
  if(!await db.SchemaReleases.AnyAsync(x=>x.Id==2)){db.SchemaReleases.Add(new(){Id=2,Name="Receipt extraction and platform workflows"});await db.SaveChangesAsync();}await tx.CommitAsync();
 }
}
