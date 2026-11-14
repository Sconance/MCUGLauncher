interface PagePlaceholderProps {
  title: string;
  description: string;
  subpage?: boolean;
}

/** 移植过渡页：页面完成前保持外壳可导航。 */
export default function PagePlaceholder({ title, description, subpage }: PagePlaceholderProps) {
  return (
    <div className={subpage ? "subpage" : "page"}>
      <div className="page-head">
        <h1 className="page-title">{title}</h1>
      </div>
      <section className="card empty">{description}</section>
    </div>
  );
}
