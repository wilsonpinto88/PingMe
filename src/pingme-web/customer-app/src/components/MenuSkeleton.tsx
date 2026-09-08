/**
 * Mirrors the real menu layout so the page does not jump when data lands.
 */
export function MenuSkeleton() {
  return (
    <div className="shell menu" aria-hidden="true">
      <div>
        <div className="skeleton skeleton--title" />
        <div className="category">
          <div className="skeleton skeleton--line" style={{ width: "30%" }} />
          <ul>
            {[0, 1, 2, 3].map((index) => (
              <li className="product product--with-image" key={index}>
                <div className="skeleton skeleton--thumb" />
                <div className="product__body">
                  <div className="skeleton skeleton--line" style={{ width: "55%" }} />
                  <div
                    className="skeleton skeleton--line"
                    style={{ width: "75%", marginTop: "var(--space-2)", height: "12px" }}
                  />
                </div>
                <div className="skeleton" style={{ width: 44, height: 44, borderRadius: "50%" }} />
              </li>
            ))}
          </ul>
        </div>
      </div>
    </div>
  );
}
