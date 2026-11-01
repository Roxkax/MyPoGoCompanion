import { useParams } from 'react-router'
import { SignedInOnly } from '../app/guards'
import { DetailScreen } from '../layout/Screen'
import { ComingSoon } from './ComingSoon'

/** E2 "All changes": back to its import report, whose address has the import's id. */
export function AllChangesScreen() {
  const { id } = useParams()
  return (
    <DetailScreen back={{ to: `/collection/imports/${id}`, label: 'Report' }} title="All changes">
      <SignedInOnly feature="collection">
        <ComingSoon
          screen="E2"
          name="All changes"
          feature="E07-F02 Re-upload and what changed"
          text="Every Pokémon that changed, arrived or left with this upload."
        />
      </SignedInOnly>
    </DetailScreen>
  )
}
