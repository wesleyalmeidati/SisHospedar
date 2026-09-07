# Vídeo do hero da Home

Coloque aqui o arquivo de vídeo que você quer usar como fundo da hero na página inicial (`/`).

## Como usar

1. Salve o arquivo como **`hero.mp4`** exatamente nesta pasta: `wwwroot/videos/hero.mp4`.
2. Recomendado: MP4 (H.264), 1920×1080 ou menor, sem áudio (o player toca mudo/looping), poucos segundos de duração em loop — vídeos de hero costumam ter 10–20s.
3. Não precisa mexer em nenhum código: `Views/Home/Index.cshtml` já referencia `~/videos/hero.mp4` e toca automaticamente (`autoplay muted loop`) assim que o arquivo existir.
4. Se o arquivo não existir (ou não carregar), a página cai automaticamente para a foto de cachoeira usada como fundo — nada quebra.

Este arquivo (`hero.mp4`) **não é versionado no Git** (está no `.gitignore` — vídeos costumam ser grandes demais pra um repositório). Só este `README.md` fica versionado, pra pasta não desaparecer no clone.
