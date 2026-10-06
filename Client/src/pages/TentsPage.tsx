import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { ApiError, api } from '../api'
import { useAuth } from '../auth'
import type { CampSite, PagedResult, ReservationResponse, Tent } from '../types'
import { RESERVATION_STATUS_CANCELLED, TENT_TYPES, TENT_TYPES_HE } from '../types'
import { ConfirmationDocument } from '../pdf/ConfirmationDocument'
import type { ConfirmationData } from '../pdf/ConfirmationDocument'
import { readStayRange, writeStayRange } from '../lib/stayDates'

// Customer-friendly price bands over the existing Tent.pricePerNight value.
const PRICE_BANDS: { id: string; label: string; match: (price: number) => boolean }[] = [
  { id: 'all', label: 'כל המחירים', match: () => true },
  { id: 'to50', label: 'עד ₪50', match: (p) => p <= 50 },
  { id: '50to100', label: '₪50–₪100', match: (p) => p > 50 && p <= 100 },
  { id: '100to150', label: '₪100–₪150', match: (p) => p > 100 && p <= 150 },
  { id: 'from150', label: 'מעל ₪150', match: (p) => p > 150 },
]

type Outcome =
  | { kind: 'ok'; text: string }
  | { kind: 'conflict'; text: string }
  | { kind: 'error'; text: string }

function isoDate(d: Date): string {
  return d.toISOString().slice(0, 10)
}

function nightsBetween(start: string, end: string): number {
  const a = Date.parse(start)
  const b = Date.parse(end)
  if (Number.isNaN(a) || Number.isNaN(b)) return 0
  return Math.round((b - a) / 86_400_000)
}

// dd/mm/yyyy from an ISO "yyyy-mm-dd" (no timezone maths - the API sends date-only).
function fmt(iso: string): string {
  if (!iso || iso.length < 10) return '—'
  const [y, m, d] = iso.split('-')
  return `${Number(d)}/${Number(m)}/${y}`
}

// Same rule the server enforces in ReservationService.EnsureTentIsFreeAsync:
// a non-cancelled reservation whose range overlaps [start, end) blocks the tent.
function findConflict(
  reservations: ReservationResponse[],
  start: string,
  end: string,
): ReservationResponse | null {
  return (
    reservations.find(
      (r) =>
        r.status !== RESERVATION_STATUS_CANCELLED && start < r.endDate && r.startDate < end,
    ) ?? null
  )
}

// The tent list with real server-side pagination, real availability (computed
// from GET /api/reservations), the booking action, and HTTP 409 handling.
// The request payloads and the 409 branching are unchanged from before; the
// PDF confirmation is offered only after a booking succeeds.
export function TentsPage() {
  const { user } = useAuth()
  // Same role check as App.tsx. Admins browse the full catalogue with no
  // capacity filter (the other filters are unchanged for everyone).
  const isAdmin = user?.role === 'Admin'

  // Arriving from the campsite map: /tents?tent=<id> focuses one tent so the
  // customer can pick dates and confirm here. The booking flow below is unchanged.
  const [searchParams] = useSearchParams()
  const focusParam = searchParams.get('tent')
  const focusTentId = focusParam && /^\d+$/.test(focusParam) ? Number(focusParam) : null

  const [data, setData] = useState<PagedResult<Tent> | null>(null)
  const [reservationsByTent, setReservationsByTent] = useState<Record<number, ReservationResponse[]>>({})
  const [sitesById, setSitesById] = useState<Record<number, CampSite>>({})
  const [loadError, setLoadError] = useState<string | null>(null)
  const [loading, setLoading] = useState(false)

  const [dates, setDates] = useState(readStayRange)
  const [openTentId, setOpenTentId] = useState<number | null>(null)
  const [outcome, setOutcome] = useState<Outcome | null>(null)
  const [booking, setBooking] = useState(false)

  const [confirmed, setConfirmed] = useState<ConfirmationData | null>(null)
  const [pdfBusy, setPdfBusy] = useState(false)
  const confRef = useRef<HTMLDivElement>(null)

  // Customer-side catalogue filters (UI only - the API is untouched).
  const [minPeople, setMinPeople] = useState(0) // 0 = any
  const [typeFilter, setTypeFilter] = useState<number | null>(null) // null = any
  const [priceBand, setPriceBand] = useState('all')
  const filtersActive =
    (!isAdmin && minPeople > 0) || typeFilter !== null || priceBand !== 'all'

  function clearFilters() {
    setMinPeople(0)
    setTypeFilter(null)
    setPriceBand('all')
  }

  function clearBookingResult() {
    setOutcome(null)
    setConfirmed(null)
  }

  const load = useCallback(async () => {
    setLoading(true)
    setLoadError(null)
    try {
      // Real server-side paging: page + pageSize go to the server. When focused
      // on one tent (from the map) we fetch just that tent instead.
      let tents: PagedResult<Tent>
      if (focusTentId != null) {
        const one = await api<Tent>(`/tents/${focusTentId}`)
        tents = {
          items: [one],
          page: 1,
          pageSize: 1,
          totalCount: 1,
          totalPages: 1,
          hasPreviousPage: false,
          hasNextPage: false,
        }
      } else {
        // One page big enough to hold the whole (single-site) catalogue so the
        // customer filters below run client-side. MaxPageSize on the API is 100.
        tents = await api<PagedResult<Tent>>('/tents?page=1&pageSize=100')
      }
      setData(tents)

      // Real availability data: the existing reservations for each visible tent.
      const byTent: Record<number, ReservationResponse[]> = {}
      const results = await Promise.all(
        tents.items.map(async (t) => {
          try {
            const r = await api<PagedResult<ReservationResponse>>(
              `/reservations?tentId=${t.id}&pageSize=100`,
            )
            return [t.id, r.items] as const
          } catch {
            return [t.id, [] as ReservationResponse[]] as const
          }
        }),
      )
      for (const [id, items] of results) byTent[id] = items
      setReservationsByTent(byTent)

      // Camp-site names for the PDF confirmation (non-fatal if it fails).
      try {
        const sites = await api<PagedResult<CampSite>>('/camping-sites?pageSize=100')
        const map: Record<number, CampSite> = {}
        for (const s of sites.items) map[s.id] = s
        setSitesById(map)
      } catch {
        /* the confirmation just omits the site line */
      }
    } catch (e) {
      setLoadError(e instanceof ApiError ? e.detail : (e as Error).message)
    } finally {
      setLoading(false)
    }
  }, [focusTentId])

  useEffect(() => {
    void load()
  }, [load])

  // Open the booking panel for the tent chosen on the map.
  useEffect(() => {
    if (focusTentId != null) {
      setOpenTentId(focusTentId)
      setOutcome(null)
      setConfirmed(null)
    }
  }, [focusTentId])

  // Remember the selected range so the campsite map checks the same dates.
  useEffect(() => {
    writeStayRange(dates)
  }, [dates])

  // An Admin has no "כמות אנשים" filter - drop any leftover selection so it can
  // never narrow the list (the filter logic also ignores it while isAdmin).
  useEffect(() => {
    if (isAdmin && minPeople !== 0) setMinPeople(0)
  }, [isAdmin, minPeople])

  function toggleBooking(tentId: number) {
    clearBookingResult()
    setOpenTentId((current) => (current === tentId ? null : tentId))
  }

  function postReservation(tentId: number): Promise<ReservationResponse> {
    return api<ReservationResponse>('/reservations', {
      method: 'POST',
      body: JSON.stringify({
        tentId,
        userId: Number(user?.id),
        startDate: dates.start,
        endDate: dates.end,
        status: 1,
      }),
    })
  }

  function rememberConfirmation(reservation: ReservationResponse, tentId: number) {
    const tent = data?.items.find((t) => t.id === tentId)
    if (!tent || !user) return
    setConfirmed({
      reservation,
      tent,
      site: sitesById[tent.campingSiteId] ?? null,
      guestName: user.fullName,
      guestEmail: user.email,
    })
  }

  async function book(tentId: number) {
    setBooking(true)
    clearBookingResult()
    try {
      const r = await postReservation(tentId)
      rememberConfirmation(r, tentId)
      setOutcome({
        kind: 'ok',
        text: `ההזמנה אושרה — מספר #${r.id}, ${fmt(r.startDate)}–${fmt(r.endDate)}, סה"כ ₪${r.totalPrice.toFixed(0)}.`,
      })
    } catch (e) {
      setOutcome(toOutcome(e))
    } finally {
      setBooking(false)
      void load()
    }
  }

  // Fires two identical bookings at once to exercise the server's optimistic-
  // concurrency guard on the tent. The loser comes back 409.
  async function bookConcurrently(tentId: number) {
    setBooking(true)
    clearBookingResult()
    try {
      const [a, b] = await Promise.allSettled([postReservation(tentId), postReservation(tentId)])
      const won = [a, b].find((r) => r.status === 'fulfilled') as
        | PromiseFulfilledResult<ReservationResponse>
        | undefined
      const lost = [a, b].find((r) => r.status === 'rejected') as PromiseRejectedResult | undefined

      if (won && lost?.reason instanceof ApiError && lost.reason.status === 409) {
        setOutcome({
          kind: 'conflict',
          text: `הזמנה #${won.value.id} נקלטה; הבקשה המקבילה קיבלה 409 והוצגה למשתמש.`,
        })
      } else if (won && lost?.reason instanceof ApiError) {
        setOutcome({
          kind: 'ok',
          text: `הזמנה #${won.value.id} נוצרה; הבקשה המקבילה נדחתה (שגיאה ${lost.reason.status}).`,
        })
        rememberConfirmation(won.value, tentId)
      } else if (won) {
        setOutcome({ kind: 'ok', text: `ההזמנה אושרה — מספר #${won.value.id}.` })
        rememberConfirmation(won.value, tentId)
      } else {
        setOutcome(toOutcome((a as PromiseRejectedResult).reason))
      }
    } finally {
      setBooking(false)
      void load()
    }
  }

  function toOutcome(e: unknown): Outcome {
    if (e instanceof ApiError && e.status === 409) {
      // HTTP 409: the tent was taken by someone else between the check and the save.
      return {
        kind: 'conflict',
        text: 'האוהל נתפס זה עתה על ידי מישהו אחר. בחרו תאריכים אחרים או אוהל אחר.',
      }
    }
    if (e instanceof ApiError) return { kind: 'error', text: `שגיאה ${e.status}: ${e.detail}` }
    return { kind: 'error', text: (e as Error).message }
  }

  async function handleDownload() {
    if (!confRef.current || !confirmed) return
    setPdfBusy(true)
    try {
      // jsPDF + html2canvas load only now, on demand.
      const { generateConfirmationPdf } = await import('../pdf/generate')
      await generateConfirmationPdf(confRef.current, confirmed.reservation.id)
    } catch (e) {
      setOutcome({ kind: 'error', text: `יצירת ה-PDF נכשלה: ${(e as Error).message}` })
    } finally {
      setPdfBusy(false)
    }
  }

  const today = isoDate(new Date())
  const nights = nightsBetween(dates.start, dates.end)
  const datesValid = nights > 0
  const refreshing = loading && data !== null

  const allTents = data?.items ?? []
  const maxCapacity = allTents.reduce((m, t) => Math.max(m, t.capacity), 0)
  const peopleOptions = Array.from({ length: maxCapacity }, (_, i) => i + 1)

  const filteredTents = useMemo(() => {
    const band = PRICE_BANDS.find((b) => b.id === priceBand) ?? PRICE_BANDS[0]
    return allTents.filter(
      (t) =>
        (isAdmin || minPeople === 0 || t.capacity >= minPeople) &&
        (typeFilter === null || t.type === typeFilter) &&
        band.match(t.pricePerNight),
    )
  }, [allTents, isAdmin, minPeople, typeFilter, priceBand])

  const shownTents = focusTentId != null ? allTents : filteredTents

  const availableCount = datesValid
    ? shownTents.filter(
        (t) => t.isActive && !findConflict(reservationsByTent[t.id] ?? [], dates.start, dates.end),
      ).length
    : 0

  return (
    <div className="page">
      {focusTentId != null && (
        <Link to="/tents" className="btn btn--ghost btn--sm backlink">
          → חזרה לכל האוהלים
        </Link>
      )}

      <div className="page-head">
        <h1 className="page-head__title">אוהלים</h1>
        <p className="page-head__sub">
          {focusTentId != null
            ? 'בחרו תאריכים ואשרו את ההזמנה לאוהל שנבחר.'
            : 'בחרו תאריכים, ראו מה פנוי, והזמינו את המקום שלכם.'}
        </p>
      </div>

      <div className="card daterange">
        <span className="daterange__label">מתי תרצו להגיע?</span>
        <div className="daterange__fields">
          <label className="field">
            <span>צ׳ק-אין</span>
            <input
              type="date"
              value={dates.start}
              min={today}
              onChange={(e) => {
                clearBookingResult()
                setDates((d) => ({ ...d, start: e.target.value }))
              }}
            />
          </label>
          <label className="field">
            <span>צ׳ק-אאוט</span>
            <input
              type="date"
              value={dates.end}
              min={dates.start || today}
              onChange={(e) => {
                clearBookingResult()
                setDates((d) => ({ ...d, end: e.target.value }))
              }}
            />
          </label>
        </div>
        <span className={datesValid ? 'daterange__nights' : 'daterange__nights daterange__nights--bad'}>
          {datesValid ? `${nights} לילות` : 'טווח תאריכים לא תקין'}
        </span>
      </div>

      {focusTentId == null && (
        <div className="card filters">
          <div className="filters__row">
            {!isAdmin && (
              <label className="field">
                <span>כמות אנשים</span>
                <select
                  value={minPeople}
                  onChange={(e) => setMinPeople(Number(e.target.value))}
                >
                  <option value={0}>הכול</option>
                  {peopleOptions.map((n) => (
                    <option key={n} value={n}>
                      {n} {n === 1 ? 'אורח' : 'אורחים'}
                    </option>
                  ))}
                </select>
              </label>
            )}

            <label className="field">
              <span>למי מיועד</span>
              <select
                value={typeFilter ?? ''}
                onChange={(e) =>
                  setTypeFilter(e.target.value === '' ? null : Number(e.target.value))
                }
              >
                <option value="">הכול</option>
                {TENT_TYPES_HE.map((he, i) => (
                  <option key={he} value={i}>
                    {he}
                  </option>
                ))}
              </select>
            </label>

            <label className="field">
              <span>מחיר ללילה</span>
              <select value={priceBand} onChange={(e) => setPriceBand(e.target.value)}>
                {PRICE_BANDS.map((b) => (
                  <option key={b.id} value={b.id}>
                    {b.label}
                  </option>
                ))}
              </select>
            </label>

            <button
              type="button"
              className="btn btn--ghost btn--sm filters__clear"
              disabled={!filtersActive}
              onClick={clearFilters}
            >
              ניקוי סינון
            </button>
          </div>

          <p className="filters__summary">
            {!data ? (
              'טוען אוהלים…'
            ) : (
              <>
                <strong>{filteredTents.length}</strong>
                {filtersActive ? ` מתוך ${allTents.length}` : ''} אוהלים
                {datesValid && filteredTents.length > 0 && (
                  <> · {availableCount} פנויים לתאריכים שנבחרו</>
                )}
              </>
            )}
            {refreshing && (
              <>
                {'  '}
                <span className="updating">
                  <span className="spinner" aria-hidden="true" /> מעדכן
                </span>
              </>
            )}
          </p>
        </div>
      )}

      {loadError && (
        <p className="banner banner--error page__alert" role="alert">
          {loadError}
        </p>
      )}

      <div className={refreshing ? 'tent-grid is-busy' : 'tent-grid'}>
        {!data && loading &&
          Array.from({ length: focusTentId != null ? 1 : 6 }, (_, i) => (
            <div key={i} className="skeleton-card" />
          ))}

        {data && shownTents.length === 0 && (
          <p className="notice">
            {allTents.length === 0
              ? 'אין אוהלים זמינים כרגע.'
              : 'לא נמצאו אוהלים שמתאימים לסינון. נסו לשנות את הבחירה או ללחוץ על "ניקוי סינון".'}
          </p>
        )}

        {shownTents.map((tent) => (
          <TentCard
            key={tent.id}
            tent={tent}
            nights={nights}
            datesValid={datesValid}
            dates={dates}
            conflict={
              tent.isActive
                ? findConflict(reservationsByTent[tent.id] ?? [], dates.start, dates.end)
                : null
            }
            open={openTentId === tent.id}
            busy={booking}
            outcome={openTentId === tent.id ? outcome : null}
            canDownloadConfirmation={
              openTentId === tent.id && outcome?.kind === 'ok' && confirmed?.tent.id === tent.id
            }
            downloadBusy={pdfBusy}
            onDownloadConfirmation={handleDownload}
            onToggle={() => toggleBooking(tent.id)}
            onBook={() => book(tent.id)}
            onBookConcurrently={() => bookConcurrently(tent.id)}
          />
        ))}
      </div>

      {/* Off-screen source for the PDF confirmation. */}
      <div aria-hidden="true" style={{ position: 'absolute', left: '-10000px', top: 0 }}>
        {confirmed && <ConfirmationDocument ref={confRef} data={confirmed} />}
      </div>
    </div>
  )
}

type TentCardProps = {
  tent: Tent
  nights: number
  datesValid: boolean
  dates: { start: string; end: string }
  conflict: ReservationResponse | null
  open: boolean
  busy: boolean
  outcome: Outcome | null
  canDownloadConfirmation: boolean
  downloadBusy: boolean
  onDownloadConfirmation: () => void
  onToggle: () => void
  onBook: () => void
  onBookConcurrently: () => void
}

function TentCard({
  tent,
  nights,
  datesValid,
  dates,
  conflict,
  open,
  busy,
  outcome,
  canDownloadConfirmation,
  downloadBusy,
  onDownloadConfirmation,
  onToggle,
  onBook,
  onBookConcurrently,
}: TentCardProps) {
  const typeKey = (TENT_TYPES[tent.type] ?? 'Standard').toLowerCase()
  const typeHe = TENT_TYPES_HE[tent.type] ?? TENT_TYPES_HE[0]
  const canBook = tent.isActive && datesValid && !conflict
  const estimate = nights > 0 ? nights * tent.pricePerNight : 0

  let availKind: 'free' | 'taken' | 'off' | 'unknown'
  let availText: string
  if (!tent.isActive) {
    availKind = 'off'
    availText = 'האוהל אינו זמין להזמנה'
  } else if (!datesValid) {
    availKind = 'unknown'
    availText = 'בחרו תאריכים כדי לבדוק זמינות'
  } else if (conflict) {
    availKind = 'taken'
    availText = `תפוס בתאריכים שנבחרו (${fmt(conflict.startDate)}–${fmt(conflict.endDate)})`
  } else {
    availKind = 'free'
    availText = 'פנוי לתאריכים שנבחרו'
  }

  return (
    <article
      className={`tent-card tent-card--${typeKey}${tent.isActive ? '' : ' tent-card--inactive'}`}
    >
      <div className="tent-card__body">
        <div className="tent-card__top">
          <span className="tent-card__label">{tent.label}</span>
          <span className={`badge badge--${typeKey}`}>{typeHe}</span>
        </div>

        <div className="tent-card__meta">
          <span className="meta__k">תפוסה</span>
          <span className="meta__price">
            <b>₪{tent.pricePerNight.toFixed(0)}</b>
            <span>ללילה</span>
          </span>
          <span className="meta__v">
            עד {tent.capacity} {tent.capacity === 1 ? 'אורח' : 'אורחים'}
          </span>
        </div>

        <span className={`avail avail--${availKind}`}>
          {(availKind === 'free' || availKind === 'taken') && (
            <span className={availKind === 'free' ? 'status-dot status-dot--on' : 'status-dot status-dot--taken'} />
          )}
          {availText}
        </span>

        <div className="tent-card__foot">
          <button
            className={open ? 'btn btn--ghost btn--block' : 'btn btn--primary btn--block'}
            disabled={!canBook && !open}
            onClick={onToggle}
          >
            {open ? 'סגירה' : conflict ? 'בחרו תאריכים אחרים' : 'הזמן אוהל'}
          </button>
        </div>
      </div>

      {open && (
        <div className="tent-card__booking">
          <p className="booking__summary">
            אוהל <strong>{tent.label}</strong> · {fmt(dates.start)}–{fmt(dates.end)} · {nights} לילות
            {' · '}
            <strong>₪{estimate.toFixed(0)}</strong> (משוער)
          </p>

          <div className="booking__actions">
            <button className="btn btn--primary" disabled={busy || !canBook} onClick={onBook}>
              {busy ? 'שולח…' : 'אישור הזמנה'}
            </button>
            <button
              type="button"
              className="booking__sim"
              disabled={busy}
              onClick={onBookConcurrently}
              title="שולח שתי הזמנות זהות בו-זמנית כדי להדגים טיפול בהתנגשות (409)"
            >
              הדמיית הזמנה כפולה
            </button>
          </div>

          {outcome && (
            <div
              className={`banner banner--${
                outcome.kind === 'ok' ? 'ok' : outcome.kind === 'conflict' ? 'warn' : 'error'
              }${canDownloadConfirmation ? ' banner--action' : ''}`}
              role="status"
            >
              <span>
                {outcome.kind === 'conflict' && <strong>התנגשות (409) — </strong>}
                {outcome.text}
              </span>
              {canDownloadConfirmation && (
                <button
                  type="button"
                  className="confirm-download"
                  disabled={downloadBusy}
                  onClick={onDownloadConfirmation}
                >
                  {downloadBusy ? 'מכין קובץ…' : 'הורדת אישור הזמנה'}
                </button>
              )}
            </div>
          )}
        </div>
      )}
    </article>
  )
}
