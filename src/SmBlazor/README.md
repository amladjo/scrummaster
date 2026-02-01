# SmBlazor (Scrum Master)

Port postojećeg statičkog JS projekta u **Blazor WebAssembly**.

## Šta je portovano

- 2-nedeljni raspored Scrum Master-a (ponedeljak–petak)
- “Who’s Out” lista (vacations)
- dayRules merge logika koja spaja susedne periode preko vikenda (koristi next work day)
- holidays + teammembers `country` (holiday može imati više država razdvojene zarezom)
- localStorage cache (najnoviji fetch se kešira + timestamp)

## Pokretanje (dev)

```cmd
cd /d C:\Data\GitHub\amladjo.github.io\src\SmBlazor
dotnet run
```

## Publish (za GitHub Pages)

Ovo je standalone WASM, pa treba samo statički output folder.

Primer publish-a u `docs/` (ako hoćeš da GitHub Pages služi iz /docs):

```cmd
cd /d C:\Data\GitHub\amladjo.github.io\src\SmBlazor
dotnet publish -c Release -o ..\..\docs
```

Napomena: za GitHub Pages routing obično se dodaje `404.html` kopija `index.html` (da refresh na ruti radi). Ako želiš, dodaću i to.

