# Avatar source images

Original 1024×1024 character portraits (see [descriptions.md](descriptions.md) for the
prompts, character names and accent colors). The game ships the processed 256px WebP
thumbnails in `src/web/public/avatars/`; the roster itself lives in
`src/web/src/avatars.ts` (keys, names, accents) and `Games/Avatars.cs` (server dedupe).

| Source | Key | Character |
|---|---|---|
| avatar01.png | `forastero` | Vicente Álvarez — El Forastero |
| avatar02.png | `viuda` | Dolores Mendoza — La Viuda |
| avatar03.png | `enterrador` | Ignacio Barbosa — El Enterrador |
| avatar04.png | `cascabel` | Luz Romero — La Cascabel |
| avatar05.png | `lobo` | El Lobo Gris |
| avatar06.png | `tahura` | Rosa Herrera — La Tahúra |
| avatar07.png | `predicador` | Salvador Quintero — El Predicador |
| avatar08.png | `cazadora` | Calista Duarte — La Cazadora |
| avatar09.png | `gambusino` | Joaquín Peralta — El Gambusino |
| avatar10.png | `contrabandista` | Perla Escamilla — La Contrabandista |

Regenerate the thumbnails with ImageMagick (bash, from the repo root):

```bash
keys=(forastero viuda enterrador cascabel lobo tahura predicador cazadora gambusino contrabandista)
for i in $(seq 1 10); do
  magick "assets/avatars/$(printf "avatar%02d.png" "$i")" -resize 256x256 -strip -quality 82 \
    "src/web/public/avatars/${keys[$((i-1))]}.webp"
done
```

Images are kept square; the frontend rounds them in CSS (circle on tiles, rounded
square in the join picker and winner ceremony).
