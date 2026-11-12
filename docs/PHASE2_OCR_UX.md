# Etapa 2/3 — OCR, ciorne și experiență de verificare (RO / EN)

**Scop:** îmbunătățirea experienței de verificare umană a bonurilor fără schimbarea regulilor Finance, aprobărilor, contabilizării sau exportului ERP. Lucrările sunt pe un branch separat față de etapa 1. Documentul descrie comportamentul implementat pe branch, nu constituie certificat de producție.

## Ce s-a schimbat

1. **Corectare sigură din original.** Pentru o regiune selectată manual, parserul nu mai alege ultimul număr când vede două valori diferite. Linia `TVA 21% 100.00 21.00` distinge baza de TVA-ul plătit numai dacă rata de pe document coincide cu câmpul cerut. Câmpurile cu mai multe date, monede sau CUI-uri rămân neinterpretate, pentru corectare manuală. Totalul urmat explicit de `ECHIVALENT/EQUIVALENT` păstrează suma principală. Nicio valoare nu este confirmată automat.
2. **Învățare responsabilă a furnizorilor.** Un șablon istoric necesită cel puțin două documente verificate separat. Nu suprascrie nici câmpuri recunoscute, nici câmpuri OCR marcate ambigue. Zona evidențiată este calculată din cuvintele găsite pe *documentul curent*, nu copiată dintr-un exemplu anterior. Sugestiile rămân `ReviewRequired`.
3. **Registrul de furnizori fără suprascriere.** Un rezultat de registru poate completa numai denumirea goală și numai dacă se potrivește CUI-ul solicitat. Textul deja introdus sau citit din OCR rămâne intact. Când numele diferă, angajatul poate selecta explicit `Folosește numele din registrul intern`; diferența este afișată.
4. **Ciorne păstrate la închidere.** În formularul pentru un bon nou, la anulare după confirmarea existentă aplicația termină salvările în curs și salvează ultima versiune în preferințele private ale utilizatorului înainte de a închide. La o eroare de rețea, păstrează formularul deschis și oferă reîncercare sau renunțare explicită. Bonul *nu* este introdus în decont până la `Salvează bonul`. Se păstrează comportamentul existent de confirmare la Escape/Anulează.
5. **Orientare și accesibilitate.** Formularul evidențiază sugestiile învățate și valorile lipsă, cu butoane de navigare la câmp. Selectorul de bonuri poate fi folosit cu Tab și săgeți stânga/dreapta, Home/End; statusul de verificare și etichetele surselor sunt mai explicite în română și engleză.

## Limite și controale păstrate

- Nu s-au introdus tabele sau migrații și nu s-a modificat conținutul documentelor originale.
- Nu s-au schimbat validările TVA/FX, segregarea rolurilor, regulile anti-duplicat, aprobările, stările documentelor sau exportul ERP.
- Registrul intern și șablonul unui furnizor sunt **sugestii**, nu dovezi că bonul și datele fiscale sunt corecte. Confirmarea utilizatorului și validarea serverului rămân obligatorii.
- Ciorna este păstrată în preferințele server-side ale contului; nu în `localStorage` și nu într-un cookie. În caz de expirare a sesiunii sau imposibilitate de conectare, recuperarea trebuie confirmată de utilizator prin autentificare și redeschiderea decontului.

## Scenarii de acceptare

- Selectează pe bon un text cu două totaluri; nu se precompletează o valoare arbitrară.
- Selectează `TVA 21% 100.00 21.00` pentru baza TVA și pentru TVA plătit: rezultatele sunt separate și corecte; ratele nepotrivite sau trei sume sunt refuzate.
- Furnizorul cu un nume scris manual nu este înlocuit automat de registru; alegerea numelui oficial este explicită.
- Un câmp OCR ambiguu rămâne ambiguu, chiar dacă există un model istoric de furnizor; indicarea sursei se face pe pagina curentă.
- Tastează date într-un bon nou, anulează și confirmă, apoi redeschide-l: ultima ciornă este restaurată. Întrerupe conexiunea înainte de salvare: formularul nu se închide fără avertizare.
- Verifică grupurile cu tastatura, pe mobil, în RO și EN, fără pierderea cursorului sau afișare de valori neconfirmate drept valori aprobate.
- CI trebuie să treacă pentru exact commitul din PR: backend pe SQLite și PostgreSQL, teste UI, build, verificarea Docker și Playwright în browser desktop/mobil.

## Condiții înainte de live

1. Rezolvă și validează independent etapa 1 (PR #19 încă neintegrat); apoi retestează ambele etape împreună.
2. Responsabilii de Finance validează documente anonimizate reale cu totaluri, TVA, CUI-uri și monede din cazurile folosite în companie. Testele sintetice nu dovedesc acuratețea reală OCR.
3. Urmează etapa 3: scenarii de producție, restore, operare, securitate și monitorizare. Niciuna dintre etapele 1–2 nu trebuie declarată singură „ready to be live”.

## Revenire și trasabilitate

Modificările acestui PR sunt additive / de comportament UI + selecție OCR, fără migrare. Revenirea la versiunea anterioară se face numai prin procedura de release aprobată, păstrând ciornele private, originalele, jurnalul de audit și volumele de date. Nu rula `docker compose down -v`.
