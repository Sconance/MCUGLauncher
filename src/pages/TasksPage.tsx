import { useState } from "react";
import { taskAction } from "../api/commands/tasks";
import { useLauncher } from "../state/launcher";
import type { TaskInfo } from "../types/launcher";
import "./TasksPage.css";

const ACTIONS = [
  ["pause", "暂停"],
  ["resume", "继续"],
  ["cancel", "取消"],
  ["delete", "删除"],
] as const;

function progressPercent(task: { progress: TaskInfo["progress"] }): number | null {
  const progress = task.progress;
  if (!progress || progress.total === null || progress.total <= 0) return null;
  return Math.min(100, Math.max(0, (progress.completed / progress.total) * 100));
}

/** 下载任务页：任务来自全局命令同步，操作通过命令提交。 */
export default function TasksPage() {
  const { tasks, refreshTasks } = useLauncher();
  const [status, setStatus] = useState("");

  const act = async (id: string, action: string) => {
    try {
      const result = await taskAction(id, action);
      await refreshTasks();
      setStatus(result.message);
    } catch (error) {
      setStatus(error instanceof Error ? error.message : String(error));
    }
  };


  return (
    <div className="subpage">
      <div className="stack" style={{ gap: 6 }}>
        <h1 className="page-title">下载任务</h1>
        <p className="muted">当前显示演示任务；重启应用会重置演示数据。</p>
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
                    {task.status} · {task.phase}
                    {progressPercent(task) === null ? "" : ` · ${Math.round(progressPercent(task) ?? 0)}%`}
                  </span>
                  {progressPercent(task) === null ? null : (
                    <div className="progress">
                      <span style={{ width: `${progressPercent(task)}%` }} />
                    </div>
                  )}
                  {task.error ? <span className="status">{task.error.message}</span> : null}
                  <div className="row" style={{ gap: 6 }}>
                    {ACTIONS.map(([action, label]) => (
                      <button
                        disabled={!task.allowedActions.includes(action)}
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
