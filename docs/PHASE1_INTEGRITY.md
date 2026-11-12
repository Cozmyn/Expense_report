# Etapa 1: integritate financiară și review-ul documentelor

Această etapă este destinată utilizării într-un mediu de test înainte de promovarea într-o instalare de producție. Nu înlocuiește testele UAT pe infrastructura reală.

## Modificări și motive

1. **Reîncercarea unei operații** — clientul păstrează același Idempotency-Key la o încercare nouă cu aceeași cerere dacă prima a pierdut răspunsul. Operațiile reușite, sau cele respinse cu erori de client, nu partajează cheia cu o operație nouă. Serverul deduplică și refuză o cheie folosită pentru alt conținut.
2. **Data, suma și moneda** — interpretarea OCR nu mai folosește automat prima dată și ultimul total. Mai multe valori incompatibile sau o ordine zi/lună neclară sunt marcate ca ambigue, iar formularul cere verificare manuală. Alternativele și sursa sunt păstrate.
3. **Completitudinea în PDF cu bonuri multiple** — o pagină atribuită nu este considerată automat completă. Utilizatorul confirmă fiecare pagină după inspectarea originalului, inclusiv bonurile omise pe aceeași pagină. Regruparea sau apariția unor noi propuneri OCR invalidează confirmările. Trimiterea la aprobare rămâne blocată până la reconfirmare.

## Compatibilitate și siguranță

- Confirmările sunt păstrate în JSON-ul existent `DocumentReviews.Dispositions`, fără modificări ale schemei. Au `Kind=CoverageConfirmed`, `Page=0` (în afara intervalului valid de pagini) și `CoveragePage` cu pagina originală. O versiune mai veche nu le poate interpreta ca justificative pentru o pagină reală.
- Un rollback de cod trebuie însoțit de verificarea datelor și a compatibilității. Înainte de rollback într-un mediu cu date reale, executați procedura aprobată de restaurare, nu ștergeți volume sau dovezi OCR.
- Datele de audit și documentele originale rămân nemodificate. Numai utilizatorul autorizat să editeze decontul poate confirma sau revoca completitudinea.
- Confirmarea de completitudine se aplică încărcărilor `Multiple` și încărcărilor `Single` separate ulterior în mai multe grupuri. Pentru o singură pagină cu bonuri multiple, numărul de pagini atribuite nu dovedește completitudinea.

## Teste de acceptare

- O cerere POST rămasă fără răspuns este retrimisă cu aceeași cheie și nu creează dubluri; aceeași cheie cu alt payload este respinsă.
- Facturile cu data scadentă înaintea datei emiterii, total EUR + echivalent RON, mai multe sume totale și format zi/lună ambiguu nu produc valori false.
- Încărcarea unui PDF multi-bon nu poate fi trimisă până când fiecare pagină este confirmată. O regrupare invalidează confirmarea.
- Testele backend trebuie să treacă pe SQLite și PostgreSQL; testele UI, browser și container trebuie să treacă pentru exact commitul livrat.
- Înainte de live: UAT și confirmarea Finance pentru documente reale, ERP, backup/restore și infrastructura Production (etapa 3).
