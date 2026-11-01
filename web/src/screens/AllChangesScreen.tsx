import { useParams } from 'react-router'
import { SignedInOnly } from '../app/guards'
import { DetailScreen } from '../layout/Screen'
import { ComingSoon } from './ComingSoon'

/** E2 "All changes": back to its import report, whose address has the import's id. */
export function AllChangesScreen() {
  const { id } = useParams()
  return (
    <DetailScreen back={{ to: `/collection/imports/${id}`, label: 'report' }} title="allChanges">
      <SignedInOnly feature="collection">
        <ComingSoon id="allChanges" screen="E2" feature="E07-F02 Re-upload and what changed" />
      </SignedInOnly>
    </DetailScreen>
  )
}
