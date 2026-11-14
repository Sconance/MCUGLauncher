import { useEffect, useState } from "react";
import { stopGame } from "../api/commands/games";
import { useLauncher } from "../state/launcher";
import "./GamesPage.css";

/** 运行中的游戏：列表来自事件，关闭通过命令提交。 */
export default function GamesPage() {
  const { games, refreshGames } = useLauncher();
  const [status, setStatus] = useState("");

  const stop = async (id: string, version: string) => {
    setStatus("正在请求关闭 " + version + "…");
    const result = await stopGame(id);
    await refreshGames();
    setStatus(result.message);
  };

  useEffect(() => {
    const timer = window.setInterval(() => {
      void refreshGames();
    }, 3000);
    return () => window.clearInterval(timer);
  }, [refreshGames]);

  return (
    <div className="subpage">
      <div className="row" style={{ gap: 10 }}>
        <h1 className="page-title">运行中的游戏</h1>
        <span className="spacer" />
        <button type="button" className="btn ghost" onClick={() => void refreshGames()}>
          刷新
        </button>
      </div>

      <div className="page-scroll" style={{ marginTop: 10 }}>
        <div className="stack" style={{ gap: 8 }}>
          <p className="muted">这里只列出由本次启动器启动、且仍在运行的游戏。关闭启动器不会自动关闭游戏。</p>
          {games.length === 0 ? (
            <div className="card muted">没有由本次启动器启动的游戏。</div>
          ) : (
            games.map((game) => (
              <article className="card game-card" key={game.id}>
                <div className="stack" style={{ gap: 2 }}>
                  <span className="game-version">{game.version}</span>
                  <span className="muted">
                    玩家 {game.player} · PID {game.pid}
                  </span>
                </div>
                <button type="button" className="btn ghost" onClick={() => void stop(game.id, game.version)}>
                  关闭游戏
                </button>
              </article>
            ))
          )}
          <span className="status">{status}</span>
        </div>
      </div>
    </div>
  );
}
