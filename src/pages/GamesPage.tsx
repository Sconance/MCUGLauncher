import { useState } from "react";
import { stopGame } from "../api/commands/games";
import { useLauncher } from "../state/launcher";
import "./GamesPage.css";

/** 运行中的游戏：列表来自全局命令同步，关闭通过命令提交。 */
export default function GamesPage() {
  const { instances: instanceCatalog, games, refreshGames } = useLauncher();
  const [status, setStatus] = useState("");

  const stop = async (sessionId: string, instanceId: string) => {
    const name = instanceCatalog?.items.find((item) => item.id === instanceId)?.displayName ?? instanceId;
    setStatus("正在请求关闭 " + name + "…");
    const result = await stopGame(sessionId);
    await refreshGames();
    setStatus(result.message);
  };


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
              <article className="card game-card" key={game.sessionId}>
                <div className="stack" style={{ gap: 2 }}>
                  <span className="game-version">
                    {instanceCatalog?.items.find((item) => item.id === game.instanceId)?.displayName ?? game.instanceId}
                  </span>
                  <span className="muted">
                    {game.status} · {game.pid === null ? "PID 待分配" : `PID ${game.pid}`}
                  </span>
                </div>
                <button
                  type="button"
                  className="btn ghost"
                  disabled={game.status !== "running"}
                  onClick={() => void stop(game.sessionId, game.instanceId)}
                >
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
