import os, pathlib

OUT = pathlib.Path(__file__).parent

# 城市 гармония口径：与游戏夜景一致的纯深蓝黑底 + 象牙/琥珀暖光
BACKGROUND = (
    "Background: a plain, very dark blue-black nocturnal gradient backdrop "
    "matching the game's night city atmosphere (deep midnight navy fading to near-black, "
    "faint smoky slate haze). One character lit by soft warm tungsten streetlight, subtle "
    "warm ivory rim light. No buildings, no environment details, no ground texture, no props, "
    "no text, no UI elements, no watermark."
)

FRAMING = (
    "Single character full-body standing illustration for character dialogue portraits in the game. "
    "Vertical composition, whole body visible from the top of the head to the feet, both feet fully "
    "seen and grounded on the bottom edge area, centered, relaxed natural standing posture facing "
    "slightly toward the viewer. Feet wear era-appropriate everyday shoes, never barefoot. Face "
    "clearly readable and emotionally expressive, suitable for portrait close-up cropping."
)

CHARACTERS = {
    "夜莺": (
        "Nightingale: a beautiful, approachable young tavern singer with soft feminine features, "
        "expressive eyes, a gentle smile, and naturally wavy dark hair. She wears a modest, slightly "
        "worn burgundy day dress and a loose cream cardigan. Charming, warm, independent, a little "
        "vulnerable, never glamorous or aristocratic."
    ),
    "艾迪": (
        "Eddie: a friendly, broad-shouldered working-class dockworker and amateur boxer, with a sturdy "
        "natural build, tousled hair, rolled-up work shirt, suspenders, worn trousers, and a relaxed, "
        "welcoming smile."
    ),
    "汤米": (
        "Tommy: a clever, ambitious young street hustler, slim and slightly awkward, with messy hair, "
        "a mischievous expression, and an oversized, ill-fitting secondhand suit. He tries hard to look "
        "important but remains unmistakably a street kid."
    ),
}

BASE_INTRO = (
    "Character concept art for a narrative-driven noir detective RPG set in a fictional 1940s-inspired "
    "harbor city at night."
)

SHARED_TAIL = (
    "Restrained saturation, human warmth within a cold and mysterious city. Hand-drawn, distinctly "
    "stylized character, emotionally expressive face, readable silhouette, appealing personality, "
    "visible everyday clothing details. No photorealism, no realistic 3D rendering, no luxury fashion, "
    "no hyper-muscular anatomy."
)

STYLES = {
    "A简化漫画": (
        "Style: simplified graphic comic / flat comic. Stylized 2D graphic novel character illustration, "
        "simplified expressive anatomy, appealing cartoon-like facial proportions, bold clean contour "
        "lines with subtle variation in thickness, large carefully designed flat color shapes, minimal "
        "cel shading, graphic facial features, expressive eyes and relaxed body language. Strong "
        "silhouette design, simplified clothing folds, clear readable expressions, elegant retro "
        "comic-book aesthetics, subtle noir atmosphere. Restrained midnight blue, dusty burgundy, smoky "
        "violet, cream and amber color palette. No realistic skin rendering, no painterly gradients, "
        "no excessive detail."
    ),
    "B手绘颗粒感": (
        "Style: rough ink / risograph. Expressive hand-drawn indie comic character art, rough organic "
        "ink outlines, uneven brush pressure, playful exaggerated facial proportions, spontaneous "
        "sketch-like details, warm imperfect character design. Limited-color risograph printing "
        "aesthetics, visible ink grain, imperfect color registration, subtle paper texture, scattered "
        "dry-brush marks and textured shadows. Flat muted ink layers of dusty rose, midnight indigo, "
        "warm ochre, smoky teal and faded charcoal. Casual expressive poses, charming imperfect smiles, "
        "intimate everyday humanity, visually rich but not realistic. Avoid polished digital rendering, "
        "smooth gradients and anatomical realism."
    ),
    "C粗黑白木刻": (
        "Style: graphic black-and-white / woodcut. Highly stylized black-and-white noir comic "
        "illustration inspired by bold linocut and woodblock printmaking. Simplified graphic human "
        "anatomy, strongly designed silhouettes, expressive cartoon-like faces, dramatic asymmetrical "
        "black shadow shapes, bold ivory highlights carved into solid dark forms. Thick irregular "
        "contours, minimal selective engraved strokes, sharp negative space, confident stylization and "
        "striking facial expressions. Almost monochrome: warm ivory and deep blue-black, with extremely "
        "restrained muted amber accents. Dramatic but approachable, graphic rather than realistic. "
        "No fine photographic shading, no grayscale airbrushing, no detailed realistic anatomy, "
        "no excessive cross-hatching."
    ),
    "E线描淡彩": (
        "Style: delicate line / muted European comic. Elegant Franco-Belgian bande dessinée character "
        "illustration, delicate hand-drawn ink outlines, lightly stylized appealing facial proportions, "
        "expressive eyes, gentle smiles, soft organic contours, subtle fine-line details and restrained "
        "hand-painted color washes. Muted watercolor-like flat colors, gentle tonal variation, visible "
        "paper texture, refined but uncomplicated clothing folds. Romantic yet grounded everyday "
        "character design, slightly whimsical, natural and emotionally intimate. Desaturated midnight "
        "blue, muted mauve, faded burgundy, warm ivory, soft amber streetlight and smoky gray. Clear "
        "readable shapes, warm humanity, understated beauty. Avoid photorealism, luxury fashion "
        "illustration, overly polished faces and complex painterly lighting."
    ),
    "F赛璐珞网点": (
        "Style: cel-shaded / halftone. Retro 2D animation character design combined with vintage "
        "comic-book halftone printing. Stylized appealing anatomy, slightly exaggerated heads and "
        "expressive eyes, clean confident ink outlines, bold simplified facial features, dynamic natural "
        "poses, two-tone cel shading and clearly separated flat colors. Carefully placed Ben-Day dots "
        "and halftone screen textures in shadow areas, subtle vintage offset-print imperfections, "
        "graphic shapes and restrained retro pop-art energy. Nocturnal palette of deep navy, smoky "
        "blue-gray, dusty red, faded plum, muted teal and warm amber. Charming and emotionally readable "
        "character, lively silhouette, sophisticated but playful noir mood. No photorealism, no glossy "
        "3D rendering, no neon cyberpunk oversaturation."
    ),
}

for cname, cdesc in CHARACTERS.items():
    for sname, sdesc in STYLES.items():
        prompt = "\n\n".join([
            BASE_INTRO,
            cdesc,
            SHARED_TAIL,
            FRAMING,
            BACKGROUND,
            sdesc,
        ])
        path = OUT / f"{cname}_{sname}-prompt.txt"
        path.write_text(prompt, encoding="utf-8")

print("wrote", len(CHARACTERS) * len(STYLES), "prompt files")
