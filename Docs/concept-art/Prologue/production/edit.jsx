export default async ({ project }) => {
  const p = await project({ dir: "higgsedit-project", size: "1920x1080", fps: 24, background: "#000000" });
  const durations = [6,5,6,7,7,8,11];
  let at = 0;
  for (let index = 0; index < durations.length; index++) {
    const source = await p.add(`media/${String(index+1).padStart(2,"0")}.mp4`);
    p.cut(source, { from: 0, dur: durations[index], at, fit: "contain" });
    at += durations[index];
  }
  await p.render("SandGuard-Prologue-raw.mp4", { depth: 8, bitrate: 12000000, concurrency: 3 });
};

