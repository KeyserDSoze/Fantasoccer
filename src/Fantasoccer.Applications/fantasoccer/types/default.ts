/**
 * Entity wrapper for Team with TeamKey
 * Matches C# Entity<Team, TeamKey> serialized as { k: TeamKey, v: Team }
 */
export interface EntityRaw<T, K> {
  k: K
  v: T
}

export interface Entity<T, K> {
    key: K;
    value: T;
}

export const mapRawEntityToEntity = <T, K, T1, K1>(raw: EntityRaw<T, K>, mapValue: (value: T) => T1, mapKey: (key: K) => K1): Entity<T1, K1> => ({
  key: mapKey(raw.k),
  value: mapValue(raw.v)
})

export const mapEntityToRawEntity = <T, K, T1, K1>(entity: Entity<T, K>, mapValue: (value: T) => T1, mapKey: (key: K) => K1): EntityRaw<T1, K1> => ({
  k: mapKey(entity.key),
  v: mapValue(entity.value)
})