interface MarkdownProps {
  text: string;
}

/** 轻量 Markdown 渲染：标题、列表、引用与段落，够公告展示使用。 */
export default function Markdown({ text }: MarkdownProps) {
  return (
    <div className="markdown">
      {text.split("\n").map((line, index) => {
        const key = `${index}-${line.length}`;
        if (line.startsWith("### ")) return <h4 key={key}>{line.slice(4)}</h4>;
        if (line.startsWith("## ")) return <h3 key={key}>{line.slice(3)}</h3>;
        if (line.startsWith("# ")) return <h2 key={key}>{line.slice(2)}</h2>;
        if (line.startsWith("> ")) return <blockquote key={key}>{line.slice(2)}</blockquote>;
        if (line.startsWith("- ")) return <li key={key}>{line.slice(2)}</li>;
        if (line.trim().length === 0) return <div key={key} className="md-gap" />;
        return <p key={key}>{line}</p>;
      })}
    </div>
  );
}
