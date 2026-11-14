import { useState } from "react";
import { taskAction } from "../api/commands/tasks";
import { useLauncher } from "../state/launcher";
import "./TasksPage.css";

const ACTIONS = [
  ["pause", "暂停"],
  ["resume", "继续"],
  ["cancel", "取消"],
  ["delete", "删除"],
] as const;

/** 下载任务页：任务来自后端事件，操作通过命令提交。 */
export default function TasksPage() {
  const { tasks, refreshTasks } = useLauncher();
  const [status, setStatus] = useState("");

  const act = async (id: string, action: string) => {
    const result = await taskAction(id, action);
    setStatus(result.message);
  };

  return (
    <div className="subpage">
      <div className="stack" style={{ gap: 6 }}>
        <h1 className="page-title">下载任务</h1>
        <p className="muted">任务由持久队列管理；关闭页面或启动器不会丢失已提交的下载。</p>
      </div>

      <div className="page-scroll" style={{ marginTop: 14 }}>
        <div className="stack" style={{ gap: 10 }}>
          <div className="stack" style={{ gap: 8 }}>
            {tasks.length === 0 ? (
              <div className="card muted">当前没有排队或运行中的任务。</div>
            ) : (
              tasks.map((task) => (
                <article className="card task-card" key={task.id}>
                  <span className="task-name">{task.name}</span>
                  <span className="muted">
                    {task.state} · {Math.round(task.percent)}%
                    {task.message.length > 0 ? ` · ${task.message}` : ""}
                  </span>
                  <div className="progress">
                    <span style={{ width: `${Math.min(100, Math.max(0, task.percent))}%` }} />
                  </div>
                  <div className="row" style={{ gap: 6 }}>
                    {ACTIONS.map(([action, label]) => (
                      <button
                        key={action}
                        type="button"
                        className="btn ghost"
                        onClick={() => void act(task.id, action)}
                      >
                        {label}
                      </button>
                    ))}
                  </div>
                </article>
              ))
            )}
          </div>

          <div className="row" style={{ gap: 10 }}>
            <button type="button" className="btn ghost" onClick={() => void refreshTasks()}>
              刷新任务
            </button>
            <span className="status">{status}</span>
          </div>
        </div>
      </div>
    </div>
  );
}
