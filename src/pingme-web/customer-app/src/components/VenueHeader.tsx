import type { VenueTheme } from "../types";

interface VenueHeaderProps {
  venueName: string;
  locationLabel: string;
  theme: VenueTheme;
}

/**
 * The venue owns this block: its logo, hero photo, colour and tagline. The
 * location badge stays constant across venues because it is the one fact the
 * customer has to be able to trust at a glance.
 */
export function VenueHeader({ venueName, locationLabel, theme }: VenueHeaderProps) {
  const hasHero = Boolean(theme.heroImageUrl);

  return (
    <header className={`venue-header${hasHero ? " venue-header--has-hero" : ""}`}>
      {theme.heroImageUrl && (
        <>
          <img
            className="venue-header__hero"
            src={theme.heroImageUrl}
            alt=""
            aria-hidden="true"
            loading="eager"
          />
          <div className="venue-header__scrim" />
        </>
      )}

      <div className="shell venue-header__inner">
        {theme.logoUrl && (
          <img
            className="venue-header__logo"
            src={theme.logoUrl}
            alt={venueName}
            height={44}
            loading="eager"
          />
        )}

        {/* With a logo present the venue name still renders for screen readers. */}
        <h1 className={theme.logoUrl ? "visually-hidden" : "venue-header__name"}>{venueName}</h1>

        {theme.tagline && <p className="venue-header__tagline">{theme.tagline}</p>}

        <p className="location-badge">
          <span className="location-badge__dot" aria-hidden="true" />
          <span>
            <span className="visually-hidden">Ordering for </span>
            {locationLabel}
          </span>
        </p>
      </div>
    </header>
  );
}
