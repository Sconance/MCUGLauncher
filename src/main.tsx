import React from "react";
import ReactDOM from "react-dom/client";
import App from "./App";
import { initBackgroundStyle } from "./shell/backgroundStyle";
import { initWallpaper } from "./shell/wallpaper";
import "./styles/tokens.css";
import "./styles/base.css";
import "./styles/components.css";
import "./styles/system.css";
import "./shell/Shell.css";
import "./styles/surface-opaque.css";

initBackgroundStyle();
initWallpaper();

ReactDOM.createRoot(document.getElementById("root") as HTMLElement).render(
  <React.StrictMode>
    <App />
  </React.StrictMode>,
);
