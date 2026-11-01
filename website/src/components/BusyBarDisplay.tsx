import {useEffect, useRef, type ReactNode} from 'react';
import useBaseUrl from '@docusaurus/useBaseUrl';
import styles from './BusyBarDisplay.module.css';

// Frames captured from a real BUSY Bar's 72 by 16 front screen while it showed the on-call theme
// with a countdown, 250 ms apart. They're stacked top to bottom in one PNG, and the last frame
// leads straight back into the first.
const COLUMNS = 72;
const ROWS = 16;
const FRAME_MS = 250;
// Shown on its own when the visitor prefers reduced motion: the countdown panel fully in view.
const STILL_FRAME = 17;

type Frames = {count: number; pixels: Uint8ClampedArray};

function draw(canvas: HTMLCanvasElement, frames: Frames, index: number, glowSource: HTMLCanvasElement) {
  const ratio = window.devicePixelRatio || 1;
  const width = canvas.clientWidth;
  const height = (width * ROWS) / COLUMNS;
  if (canvas.width !== Math.round(width * ratio)) {
    canvas.width = Math.round(width * ratio);
    canvas.height = Math.round(height * ratio);
  }
  const ctx = canvas.getContext('2d');
  if (!ctx) return;

  const pitch = canvas.width / COLUMNS;
  const offset = index * COLUMNS * ROWS * 4;
  ctx.fillStyle = '#0c0d0f';
  ctx.fillRect(0, 0, canvas.width, canvas.height);

  // A blurred copy of the frame underneath the dots stands in for the light bleeding from each LED.
  const glowCtx = glowSource.getContext('2d');
  if (glowCtx) {
    glowCtx.putImageData(new ImageData(frames.pixels.slice(offset, offset + COLUMNS * ROWS * 4), COLUMNS, ROWS), 0, 0);
    ctx.save();
    ctx.filter = `blur(${pitch * 0.9}px)`;
    ctx.globalAlpha = 0.55;
    ctx.imageSmoothingEnabled = true;
    ctx.drawImage(glowSource, 0, 0, canvas.width, canvas.height);
    ctx.restore();
  }

  const radius = pitch * 0.38;
  for (let y = 0; y < ROWS; y++) {
    for (let x = 0; x < COLUMNS; x++) {
      const i = offset + (y * COLUMNS + x) * 4;
      const r = frames.pixels[i];
      const g = frames.pixels[i + 1];
      const b = frames.pixels[i + 2];
      ctx.fillStyle = r + g + b < 30 ? '#1b1c21' : `rgb(${r},${g},${b})`;
      ctx.beginPath();
      ctx.arc((x + 0.5) * pitch, (y + 0.5) * pitch, radius, 0, Math.PI * 2);
      ctx.fill();
    }
  }
}

export default function BusyBarDisplay(): ReactNode {
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const src = useBaseUrl('/img/busybar-on-call.png');

  useEffect(() => {
    const canvas = canvasRef.current;
    if (!canvas) return undefined;

    let frames: Frames | null = null;
    let index = 0;
    let timer: number | undefined;
    const reduceMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    const glowSource = document.createElement('canvas');
    glowSource.width = COLUMNS;
    glowSource.height = ROWS;

    const render = () => frames && draw(canvas, frames, index, glowSource);
    const resize = new ResizeObserver(render);
    resize.observe(canvas);

    const image = new Image();
    image.onload = () => {
      const reader = document.createElement('canvas');
      reader.width = image.width;
      reader.height = image.height;
      const ctx = reader.getContext('2d');
      if (!ctx) return;
      ctx.drawImage(image, 0, 0);
      frames = {count: image.height / ROWS, pixels: ctx.getImageData(0, 0, image.width, image.height).data};
      index = reduceMotion ? STILL_FRAME : 0;
      render();
      if (!reduceMotion) {
        timer = window.setInterval(() => {
          index = (index + 1) % (frames?.count ?? 1);
          render();
        }, FRAME_MS);
      }
    };
    image.src = src;

    return () => {
      resize.disconnect();
      window.clearInterval(timer);
    };
  }, [src]);

  return (
    <figure className={styles.device}>
      <div className={styles.screen}>
        <canvas
          ref={canvasRef}
          className={styles.matrix}
          role="img"
          aria-label="A BUSY Bar showing ON CALL, with the time left sliding in every few seconds"
        />
      </div>
    </figure>
  );
}
